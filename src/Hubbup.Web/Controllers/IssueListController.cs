using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Hubbup.Web.Models;
using Hubbup.Web.Utils;
using Hubbup.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NuGet.Versioning;
using Octokit;

namespace Hubbup.Web.Controllers
{
    public class IssueListController : Controller, IGitHubQueryProvider
    {
        public IssueListController(IRepoSetProvider repoSetProvider, IPersonSetProvider personSetProvider, UrlEncoder urlEncoder)
        {
            RepoSetProvider = repoSetProvider;
            PersonSetProvider = personSetProvider;
            UrlEncoder = urlEncoder;
        }

        public IRepoSetProvider RepoSetProvider { get; }

        public IPersonSetProvider PersonSetProvider { get; private set; }

        public UrlEncoder UrlEncoder { get; }

        private RepoTask<IReadOnlyList<Issue>> GetIssuesForRepo(RepoDefinition repo, IGitHubClient gitHubClient)
        {
            var repositoryIssueRequest = new RepositoryIssueRequest
            {
                State = ItemStateFilter.Open,
            };

            return new RepoTask<IReadOnlyList<Issue>>
            {
                Repo = repo,
                Task = gitHubClient.Issue.GetAllForRepository(repo.Owner, repo.Name, repositoryIssueRequest),
            };
        }

        private RepoTask<IReadOnlyList<PullRequest>> GetPullRequestsForRepo(RepoDefinition repo, IGitHubClient gitHubClient)
        {
            return new RepoTask<IReadOnlyList<PullRequest>>
            {
                Repo = repo,
                Task = gitHubClient.PullRequest.GetAllForRepository(repo.Owner, repo.Name),
            };
        }

        private static readonly string[] ExcludedMilestones = new[] {
            "Backlog",
            "Discussion",
            "Discussions",
            "Future",
        };

        private static bool IsExcludedMilestone(string repoName)
        {
            return ExcludedMilestones.Contains(repoName, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<DateTimeOffset?> GetWorkingStartTime(RepoDefinition repo, Issue issue, string[] workingLabels, IGitHubClient gitHubClient)
        {
            var workingLabelsOnThisIssue =
                issue.Labels
                    .Where(label => workingLabels.Contains(label.Name, StringComparer.OrdinalIgnoreCase))
                    .Select(label => label.Name);

            if (!workingLabelsOnThisIssue.Any())
            {
                // Item isn't in any Working state, so ignore it
                return null;
            }

            // Find all "labeled" events for this issue
            var issueEvents = await gitHubClient.Issue.Events.GetAllForIssue(repo.Owner, repo.Name, issue.Number);

            foreach (var workingLabelOnThisIssue in workingLabelsOnThisIssue)
            {
                var labelEvent = issueEvents.LastOrDefault(
                    issueEvent =>
                        issueEvent.Event == EventInfoState.Labeled &&
                        string.Equals(issueEvent.Label.Name, workingLabelOnThisIssue, StringComparison.OrdinalIgnoreCase));

                if (labelEvent != null)
                {
                    // If an event where this label was applied was found, return the date on which it was applied
                    return labelEvent.CreatedAt;
                }
            }

            return null;
        }

        [Route("{repoSet}")]
        [Authorize]
        public async Task<IActionResult> Index(string repoSet)
        {
            var gitHubName = HttpContext.User.Identity.Name;
            var gitHubAccessToken = await HttpContext.Authentication.GetTokenAsync("access_token");
            // Authenticated and all claims have been read

            if (!RepoSetProvider.RepoSetExists(repoSet))
            {
                return NotFound();
            }

            var requestStopwatch = new Stopwatch();
            requestStopwatch.Start();

            var repos = RepoSetProvider.GetRepoSet(repoSet);
            var distinctRepos =
                repos.Repos
                    .Distinct()
                    .Where(repo => repo.RepoInclusionLevel != RepoInclusionLevel.None)
                    .ToArray();
            var personSetName = repos.AssociatedPersonSetName;
            var personSet = PersonSetProvider.GetPersonSet(personSetName);
            var peopleInPersonSet = personSet?.People ?? new string[0];
            var workingLabels = repos.WorkingLabels ?? new string[0];

            var allIssuesByRepo = new ConcurrentDictionary<RepoDefinition, RepoTask<IReadOnlyList<Issue>>>();
            var allPullRequestsByRepo = new ConcurrentDictionary<RepoDefinition, RepoTask<IReadOnlyList<PullRequest>>>();

            var gitHubClient = GitHubUtils.GetGitHubClient(gitHubAccessToken);


            // Get missing repos
            var distinctOrgs =
                distinctRepos
                    .Select(
                        repoDefinition => repoDefinition.Owner)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(org => org)
                    .ToList();

            var allOrgRepos = new ConcurrentDictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            var getAllOrgReposTask = AsyncParallelUtils.ForEachAsync(distinctOrgs, 5, async org =>
            {
                var reposInOrg = await gitHubClient.Repository.GetAllForOrg(org);
                allOrgRepos[org] = reposInOrg.Where(repo => !repo.Fork).Select(repo => repo.Name).ToArray();
            });
            await getAllOrgReposTask;

            var missingOrgRepos = allOrgRepos.Select(org =>
                new MissingRepoSet
                {
                    Org = org.Key,
                    MissingRepos =
                        org.Value
                            .Except(
                                distinctRepos
                                    .Select(repoDefinition => repoDefinition.Name), StringComparer.OrdinalIgnoreCase)
                            .OrderBy(repo => repo, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                })
                .OrderBy(missingRepoSet => missingRepoSet.Org, StringComparer.OrdinalIgnoreCase)
                .ToList();


            // Get bugs/PR data
            Parallel.ForEach(distinctRepos, repo => allIssuesByRepo[repo] = GetIssuesForRepo(repo, gitHubClient));
            Parallel.ForEach(distinctRepos, repo => allPullRequestsByRepo[repo] = GetPullRequestsForRepo(repo, gitHubClient));

            // while waiting for queries to run, do some other work...

            var distinctMainRepos = distinctRepos.Where(repo => repo.RepoInclusionLevel == RepoInclusionLevel.AllItems).ToArray();
            var distinctExtraRepos = distinctRepos.Where(repo => repo.RepoInclusionLevel == RepoInclusionLevel.ItemsAssignedToPersonSet).ToArray();

            var labelQuery = GetLabelQuery(repos.LabelFilter);

            var openIssuesQuery = GetOpenIssuesQuery(GetExcludedMilestonesQuery(), labelQuery, distinctMainRepos);
            var workingIssuesQuery = GetWorkingIssuesQuery(labelQuery, workingLabels, distinctMainRepos);
            var unassignedIssuesQuery = GetUnassignedIssuesQuery(GetExcludedMilestonesQuery(), labelQuery, distinctMainRepos);
            var untriagedIssuesQuery = GetUntriagedIssuesQuery(labelQuery, distinctMainRepos);
            var openPRsQuery = GetOpenPRsQuery(distinctMainRepos);
            var stalePRsQuery = GetStalePRsQuery(distinctMainRepos);

            // now wait for queries to finish executing

            try
            {
                Task.WaitAll(allIssuesByRepo.Select(x => x.Value.Task).ToArray());
            }
            catch (AggregateException)
            {
                // Just hide the exceptions here - faulted tasks will be aggregated later
            }

            try
            {
                Task.WaitAll(allPullRequestsByRepo.Select(x => x.Value.Task).ToArray());
            }
            catch (AggregateException)
            {
                // Just hide the exceptions here - faulted tasks will be aggregated later
            }

            var repoFailures = new List<RepoFailure>();
            repoFailures.AddRange(
                allIssuesByRepo
                    .Where(repoTask => repoTask.Value.Task.IsFaulted || repoTask.Value.Task.IsCanceled)
                    .Select(repoTask =>
                        new RepoFailure
                        {
                            Repo = repoTask.Key,
                            FailureMessage = string.Format("Issues couldn't be retrieved for the {0}/{1} repo", repoTask.Key.Owner, repoTask.Key.Name),
                        }));
            repoFailures.AddRange(
                allPullRequestsByRepo
                    .Where(repoTask => repoTask.Value.Task.IsFaulted || repoTask.Value.Task.IsCanceled)
                    .Select(repoTask =>
                        new RepoFailure
                        {
                            Repo = repoTask.Key,
                            FailureMessage = string.Format("Pull requests couldn't be retrieved for the {0}/{1} repo", repoTask.Key.Owner, repoTask.Key.Name),
                        }));

            var allIssues = allIssuesByRepo
                .Where(repoTask => !repoTask.Value.Task.IsFaulted && !repoTask.Value.Task.IsCanceled)
                .SelectMany(issueList =>
                    issueList.Value.Task.Result
                        .Where(
                            issue =>
                                !IsExcludedMilestone(issue.Milestone?.Title) &&
                                issue.PullRequest == null &&
                                IsFilteredIssue(issue, repos) &&
                                ItemIncludedByInclusionLevel(issue.Assignee?.Login, issueList.Key, peopleInPersonSet))
                        .Select(
                            issue => new IssueWithRepo
                            {
                                Issue = issue,
                                Repo = issueList.Key,
                                WorkingStartTime = GetWorkingStartTime(issueList.Key, issue, workingLabels, gitHubClient).Result,
                                IsInAssociatedPersonSet = IsInAssociatedPersonSet(issue.Assignee?.Login, personSet),
                            }))
                .OrderBy(issueWithRepo => issueWithRepo.WorkingStartTime)
                .ToList();

            var workingIssues = allIssues
                .Where(issue =>
                    issue.Issue.Labels
                        .Any(label => workingLabels.Contains(label.Name, StringComparer.OrdinalIgnoreCase)))
                .ToList();

            var untriagedIssues = allIssues
                .Where(issue => issue.Issue.Milestone == null).ToList();

            var unassignedIssues = allIssues
                .Where(issue => issue.Issue.Assignee == null).ToList();

            var allPullRequests = allPullRequestsByRepo
                .Where(repoTask => !repoTask.Value.Task.IsFaulted && !repoTask.Value.Task.IsCanceled)
                .SelectMany(pullRequestList =>
                    pullRequestList.Value.Task.Result
                        .Where(
                            pullRequest => !IsExcludedMilestone(pullRequest.Milestone?.Title) &&
                            (ItemIncludedByInclusionLevel(pullRequest.Assignee?.Login, pullRequestList.Key, peopleInPersonSet) ||
                            ItemIncludedByInclusionLevel(pullRequest.User.Login, pullRequestList.Key, peopleInPersonSet)))
                        .Select(pullRequest =>
                            new PullRequestWithRepo
                            {
                                PullRequest = pullRequest,
                                Repo = pullRequestList.Key,
                                IsInAssociatedPersonSet = IsInAssociatedPersonSet(pullRequest.User?.Login, personSet),
                            }))
                .OrderBy(pullRequestWithRepo => pullRequestWithRepo.PullRequest.CreatedAt)
                .ToList();


            var allIssuesInMainRepos = allIssues.Where(issue => distinctMainRepos.Contains(issue.Repo)).ToList();
            var allIssuesInExtraRepos = allIssues.Where(issue => distinctExtraRepos.Contains(issue.Repo)).ToList();


            var mainMilestoneData = distinctMainRepos
                .OrderBy(repo => repo.Owner + "/" + repo.Name, StringComparer.OrdinalIgnoreCase)
                .Select(repo =>
                    new MilestoneSummary()
                    {
                        Repo = repo,
                        MilestoneData = allIssuesInMainRepos
                            .Where(issue => issue.Repo == repo)
                            .GroupBy(issue => issue.Issue.Milestone?.Title)
                            .Select(issueMilestoneGroup => new MilestoneData
                            {
                                Milestone = issueMilestoneGroup.Key,
                                OpenIssues = issueMilestoneGroup.Count(),
                            })
                            .ToList(),
                    });
            var fullSortedMainMilestoneList = mainMilestoneData
                .SelectMany(milestone => milestone.MilestoneData)
                .Select(milestone => milestone.Milestone)
                .Distinct()
                .OrderBy(milestone => new PossibleSemanticVersion(milestone));

            var extraMilestoneData = distinctExtraRepos
                .OrderBy(repo => repo.Owner + "/" + repo.Name, StringComparer.OrdinalIgnoreCase)
                .Select(repo =>
                    new MilestoneSummary()
                    {
                        Repo = repo,
                        MilestoneData = allIssuesInExtraRepos
                            .Where(issue => issue.Repo == repo)
                            .GroupBy(issue => issue.Issue.Milestone?.Title)
                            .Select(issueMilestoneGroup => new MilestoneData
                            {
                                Milestone = issueMilestoneGroup.Key,
                                OpenIssues = issueMilestoneGroup.Count(),
                            })
                            .ToList(),
                    });
            var fullSortedExtraMilestoneList = extraMilestoneData
                .SelectMany(milestone => milestone.MilestoneData)
                .Select(milestone => milestone.Milestone)
                .Distinct()
                .OrderBy(milestone => new PossibleSemanticVersion(milestone));

            var issueListViewModel = new IssueListViewModel
            {
                RepoFailures = repoFailures,

                GitHubUserName = gitHubName,
                LastUpdated = DateTimeOffset.Now.ToPacificTime().ToString(),

                ExtraLinks = repos.RepoExtraLinks,

                RepoSetName = repoSet,
                RepoSetNames = RepoSetProvider.GetRepoSetLists().Select(repoSetList => repoSetList.Key).ToArray(),

                TotalIssues = allIssues.Where(issue => issue.Repo.RepoInclusionLevel == RepoInclusionLevel.AllItems).Count(),
                WorkingIssues = workingIssues.Count,
                UntriagedIssues = untriagedIssues.Where(issue => issue.Repo.RepoInclusionLevel == RepoInclusionLevel.AllItems).Count(),
                UnassignedIssues = unassignedIssues.Count,
                OpenPullRequests = allPullRequests.Where(pr => pr.Repo.RepoInclusionLevel == RepoInclusionLevel.AllItems).Count(),
                StalePullRequests = allPullRequests.Where(pr => pr.Repo.RepoInclusionLevel == RepoInclusionLevel.AllItems && pr.PullRequest.CreatedAt < DateTimeOffset.Now.AddDays(-14)).Count(),

                MainReposIncluded = distinctMainRepos.GetRepoSummary(allIssues, workingIssues, allPullRequests, labelQuery, workingLabels, this),
                ExtraReposIncluded = distinctExtraRepos.GetRepoSummary(allIssues, workingIssues, allPullRequests, labelQuery, workingLabels, this),
                MissingRepos = missingOrgRepos,

                MainMilestoneSummary = new MilestoneSummaryData
                {
                    MilestoneData = mainMilestoneData.ToList(),
                    MilestonesAvailable = fullSortedMainMilestoneList.ToList(),
                },
                ExtraMilestoneSummary = new MilestoneSummaryData
                {
                    MilestoneData = extraMilestoneData.ToList(),
                    MilestonesAvailable = fullSortedExtraMilestoneList.ToList(),
                },

                OpenIssuesQuery = openIssuesQuery,
                WorkingIssuesQuery = workingIssuesQuery,
                UntriagedIssuesQuery = untriagedIssuesQuery,
                UnassignedIssuesQuery = unassignedIssuesQuery,
                OpenPRsQuery = openPRsQuery,
                StalePRsQuery = stalePRsQuery,

                GroupByAssignee = new GroupByAssigneeViewModel
                {
                    Assignees =
                        peopleInPersonSet
                            .OrderBy(person => person, StringComparer.OrdinalIgnoreCase)
                            .Select(person =>
                                new GroupByAssigneeAssignee
                                {
                                    Assignee = person,
                                    IsInAssociatedPersonSet = IsInAssociatedPersonSet(person, personSet),
                                    Issues = workingIssues
                                        .Where(workingIssue =>
                                            workingIssue.Issue.Assignee?.Login == person)
                                        .ToList(),
                                    PullRequests = allPullRequests
                                        .Where(
                                            pr =>
                                                pr.PullRequest.User.Login == person ||
                                                pr.PullRequest.Assignee?.Login == person)
                                        .OrderBy(pr => pr.PullRequest.CreatedAt)
                                        .ToList(),
                                    OtherIssues = allIssues
                                        .Where(issue =>
                                            issue.Issue.Assignee?.Login == person)
                                        .Except(workingIssues)
                                        .OrderBy(issueWithRepo => new PossibleSemanticVersion(issueWithRepo.Issue.Milestone?.Title))
                                        .ThenBy(issueWithRepo => issueWithRepo.Repo.Name, StringComparer.OrdinalIgnoreCase)
                                        .ThenBy(issueWithRepo => issueWithRepo.Issue.Number)
                                        .ToList(),
                                })
                            .Concat(new[]
                            {
                                new GroupByAssigneeAssignee
                                {
                                    Assignee = "<other assignees>",
                                    IsMetaAssignee = true,
                                    IsInAssociatedPersonSet = false,
                                    Issues = workingIssues
                                        .Where(workingIssue =>
                                            workingIssue.Issue.Assignee != null &&
                                            !peopleInPersonSet.Contains(workingIssue.Issue.Assignee.Login, StringComparer.OrdinalIgnoreCase))
                                        .ToList(),
                                    PullRequests = allPullRequests
                                        .Where(
                                            pr =>
                                                pr.PullRequest.Assignee != null &&
                                                !peopleInPersonSet.Contains(pr.PullRequest.User.Login, StringComparer.OrdinalIgnoreCase) &&
                                                !peopleInPersonSet.Contains(pr.PullRequest.Assignee.Login, StringComparer.OrdinalIgnoreCase))
                                        .OrderBy(pr => pr.PullRequest.CreatedAt)
                                        .ToList(),
                                    OtherIssues = allIssues
                                        .Where(issue =>
                                            issue.Issue.Assignee != null &&
                                            !peopleInPersonSet.Contains(issue.Issue.Assignee?.Login, StringComparer.OrdinalIgnoreCase))
                                        .Except(workingIssues)
                                        .OrderBy(issueWithRepo => issueWithRepo.Issue.Assignee.Login, StringComparer.OrdinalIgnoreCase)
                                        .ThenBy(issueWithRepo => new PossibleSemanticVersion(issueWithRepo.Issue.Milestone?.Title))
                                        .ThenBy(issueWithRepo => issueWithRepo.Repo.Name, StringComparer.OrdinalIgnoreCase)
                                        .ThenBy(issueWithRepo => issueWithRepo.Issue.Number)
                                        .ToList(),
                                },
                                new GroupByAssigneeAssignee
                                {
                                    Assignee = "<unassigned>",
                                    IsMetaAssignee = true,
                                    IsInAssociatedPersonSet = false,
                                    Issues = workingIssues
                                        .Where(workingIssue =>
                                            workingIssue.Issue.Assignee == null)
                                        .ToList(),
                                    PullRequests = allPullRequests
                                        .Where(
                                            pr =>
                                                pr.PullRequest.Assignee == null &&
                                                !peopleInPersonSet.Contains(pr.PullRequest.User.Login, StringComparer.OrdinalIgnoreCase))
                                        .OrderBy(pr => pr.PullRequest.CreatedAt)
                                        .ToList(),
                                    OtherIssues = allIssues
                                        .Where(issue => issue.Issue.Assignee == null)
                                        .Except(workingIssues)
                                        .OrderBy(issueWithRepo => new PossibleSemanticVersion(issueWithRepo.Issue.Milestone?.Title))
                                        .ThenBy(issueWithRepo => issueWithRepo.Repo.Name, StringComparer.OrdinalIgnoreCase)
                                        .ThenBy(issueWithRepo => issueWithRepo.Issue.Number)
                                        .ToList(),
                                },
                            })
                            .ToList()
                            .AsReadOnly(),
                },

                GroupByMilestone = new GroupByMilestoneViewModel
                {
                    Milestones =
                        workingIssues
                            .Select(issue => issue.Issue.Milestone?.Title)
                            .Concat(new string[] { null })
                            .Distinct()
                            .OrderBy(milestone => new PossibleSemanticVersion(milestone))
                            .Select(milestone =>
                                new GroupByMilestoneMilestone
                                {
                                    Milestone = milestone,
                                    Issues = workingIssues
                                        .Where(issue => issue.Issue.Milestone?.Title == milestone)
                                        .OrderBy(issue => issue.WorkingStartTime)
                                        .ToList(),
                                    PullRequests = allPullRequests
                                        .Where(pullRequest => pullRequest.PullRequest.Milestone?.Title == milestone)
                                            .OrderBy(pullRequest => pullRequest.PullRequest.CreatedAt)
                                        .ToList(),
                                })
                            .OrderBy(group => new PossibleSemanticVersion(group.Milestone))
                            .ToList()
                },

                GroupByRepo = new GroupByRepoViewModel
                {
                    Repos =
                        workingIssues
                            .Select(issue => issue.Repo)
                            .Concat(allPullRequests.Select(pullRequest => pullRequest.Repo))
                            .Distinct()
                            .OrderBy(repo => repo)
                            .Select(repo =>
                                new GroupByRepoRepo
                                {
                                    Repo = repo,
                                    Issues = workingIssues
                                        .Where(issue => issue.Repo == repo)
                                        .OrderBy(issue => issue.WorkingStartTime)
                                        .ToList(),
                                    PullRequests = allPullRequests
                                        .Where(pullRequest => pullRequest.Repo == repo)
                                        .OrderBy(pullRequest => pullRequest.PullRequest.Assignee?.Login)
                                        .ThenBy(pullRequest => pullRequest.PullRequest.Number)
                                        .ToList(),
                                })
                            .ToList()
                },
            };

            requestStopwatch.Stop();
            issueListViewModel.PageRequestTime = requestStopwatch.Elapsed;

            return View(issueListViewModel);
        }

        private bool ItemIncludedByInclusionLevel(string itemAssignee, RepoDefinition repo, string[] peopleInPersonSet)
        {
            if (repo.RepoInclusionLevel == RepoInclusionLevel.AllItems)
            {
                return true;
            }
            if (repo.RepoInclusionLevel == RepoInclusionLevel.ItemsAssignedToPersonSet)
            {
                return peopleInPersonSet.Contains(itemAssignee, StringComparer.OrdinalIgnoreCase);
            }
            return false;
        }

        private static string GetLabelQuery(string labelFilter)
        {
            if (string.IsNullOrEmpty(labelFilter))
            {
                return string.Empty;
            }
            return "label:" + labelFilter;
        }

        private static bool IsFilteredIssue(Issue issue, RepoSetDefinition repos)
        {
            if (repos.LabelFilter == null)
            {
                // If there's no label filter, allow all items
                return true;
            }

            return issue.Labels.Any(label => label.Name == repos.LabelFilter);
        }

        private static bool IsInAssociatedPersonSet(string userLogin, PersonSet personSet)
        {
            if (personSet == null)
            {
                // If there's no person set, assume the person is in
                return true;
            }
            if (userLogin == null)
            {
                // If there's no assignee, mark the person as out
                return false;
            }
            return personSet.People.Contains(userLogin, StringComparer.OrdinalIgnoreCase);
        }

        public string GetExcludedMilestonesQuery()
        {
            return string.Join(" ", ExcludedMilestones.Select(milestone => "-milestone:\"" + milestone + "\""));
        }

        private static string GetStalePRDate()
        {
            var staleDays = 14;
            var stalePRDate = DateTimeOffset.UtcNow.ToPacificTime().AddDays(-staleDays);
            // GitHub uses the format 'YYYY-MM-DD'
            return stalePRDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string GetRepoQuery(params RepoDefinition[] repos)
        {
            return string.Join(" ", repos.Select(repo => "repo:" + repo.Owner + "/" + repo.Name));
        }

        private string GetGitHubQuery(params string[] rawQueryParts)
        {
            const string GitHubQueryPrefix = "https://github.com/search?q=";

            return GitHubQueryPrefix + UrlEncoder.Encode(string.Join(" ", rawQueryParts)) + "&s=updated";
        }

        public string GetOpenIssuesQuery(string excludedMilestonesQuery, string labelQuery, params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:issue", "is:open", GetRepoQuery(repos), excludedMilestonesQuery, labelQuery);
        }

        public string GetWorkingIssuesQuery(string labelQuery, string[] workingLabels, params RepoDefinition[] repos)
        {
            // TODO: No way to do a query for "label:L1 OR label:L2" so we just pick the first label, if any
            var workingLabelsQuery = string.Empty;
            var firstWorkingLabel = workingLabels.FirstOrDefault();
            if (firstWorkingLabel != null)
            {
                workingLabelsQuery = $"label:\"{firstWorkingLabel}\"";
            }
            return GetGitHubQuery("is:issue", "is:open", workingLabelsQuery, GetRepoQuery(repos), labelQuery);
        }

        public string GetUntriagedIssuesQuery(string labelQuery, params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:issue", "is:open", "no:milestone", GetRepoQuery(repos), labelQuery);
        }

        public string GetUnassignedIssuesQuery(string excludedMilestonesQuery, string labelQuery, params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:issue", "is:open", "no:assignee", GetRepoQuery(repos), excludedMilestonesQuery, labelQuery);
        }

        public string GetOpenPRsQuery(params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:pr", "is:open", GetRepoQuery(repos), GetExcludedMilestonesQuery());
        }

        public string GetStalePRsQuery(params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:pr", "is:open", "created:<=" + GetStalePRDate(), GetRepoQuery(repos), GetExcludedMilestonesQuery());
        }

        private class RepoTask<TTaskResult>
        {
            public RepoDefinition Repo { get; set; }
            public Task<TTaskResult> Task { get; set; }
        }

        /// <summary>
        /// Container for comparing milestones that might be semantic versions, or
        /// might be arbitrary strings.
        /// </summary>
        private class PossibleSemanticVersion : IComparable<PossibleSemanticVersion>
        {
            public PossibleSemanticVersion(string possibleSemanticVersion)
            {
                NuGetVersion nuGetVersion;
                if (NuGetVersion.TryParse(possibleSemanticVersion, out nuGetVersion))
                {
                    NuGetVersion = nuGetVersion;
                }
                else
                {
                    NonSemanticVersion = possibleSemanticVersion;
                }
            }

            public string NonSemanticVersion { get; }

            public NuGetVersion NuGetVersion { get; }

            public int CompareTo(PossibleSemanticVersion other)
            {
                if (other == null)
                {
                    return 1;
                }

                if (NuGetVersion != null)
                {
                    if (other.NuGetVersion != null)
                    {
                        return NuGetVersion.CompareTo(other.NuGetVersion);
                    }
                    else
                    {
                        return 1;
                    }
                }
                else
                {
                    if (other.NuGetVersion != null)
                    {
                        return -1;
                    }
                    else
                    {
                        return string.Compare(NonSemanticVersion, other.NonSemanticVersion, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }
    }

    public static class MyExt
    {
        public static List<RepoSummary> GetRepoSummary(
            this IEnumerable<RepoDefinition> repos,
            List<IssueWithRepo> allIssues,
            List<IssueWithRepo> workingIssues,
            List<PullRequestWithRepo> allPullRequests,
            string labelQuery,
            string[] workingLabels,
            IGitHubQueryProvider gitHubQueryProvider)
        {
            return repos
                .OrderBy(repo => repo.Owner.ToLowerInvariant())
                .ThenBy(repo => repo.Name.ToLowerInvariant())
                .Select(repo => new RepoSummary
                {
                    Repo = repo,
                    OpenIssues = allIssues.Where(issue => issue.Repo == repo).Count(),
                    OpenIssuesQueryUrl = gitHubQueryProvider.GetOpenIssuesQuery(gitHubQueryProvider.GetExcludedMilestonesQuery(), labelQuery, repo),
                    UnassignedIssues = allIssues.Where(issue => issue.Repo == repo && issue.Issue.Assignee == null).Count(),
                    UnassignedIssuesQueryUrl = gitHubQueryProvider.GetUnassignedIssuesQuery(gitHubQueryProvider.GetExcludedMilestonesQuery(), labelQuery, repo),
                    UntriagedIssues = allIssues.Where(issue => issue.Repo == repo && issue.Issue.Milestone == null).Count(),
                    UntriagedIssuesQueryUrl = gitHubQueryProvider.GetUntriagedIssuesQuery(labelQuery, repo),
                    WorkingIssues = allIssues.Where(issue => issue.Repo == repo && workingIssues.Contains(issue)).Count(),
                    WorkingIssuesQueryUrl = gitHubQueryProvider.GetWorkingIssuesQuery(labelQuery, workingLabels, repo),
                    OpenPRs = allPullRequests.Where(pullRequest => pullRequest.Repo == repo).Count(),
                    OpenPRsQueryUrl = gitHubQueryProvider.GetOpenPRsQuery(repo),
                    StalePRs = allPullRequests.Where(pullRequest => pullRequest.Repo == repo && pullRequest.PullRequest.CreatedAt < DateTimeOffset.Now.AddDays(-14)).Count(),
                    StalePRsQueryUrl = gitHubQueryProvider.GetStalePRsQuery(repo),
                })
                .ToList();
        }
    }

    public interface IGitHubQueryProvider
    {
        string GetOpenIssuesQuery(string excludedMilestonesQuery, string labelQuery, params RepoDefinition[] repos);
        string GetExcludedMilestonesQuery();
        string GetUnassignedIssuesQuery(string excludedMilestonesQuery, string labelQuery, params RepoDefinition[] repos);
        string GetUntriagedIssuesQuery(string labelQuery, params RepoDefinition[] repos);
        string GetWorkingIssuesQuery(string labelQuery, string[] workingLabels, params RepoDefinition[] repos);
        string GetOpenPRsQuery(params RepoDefinition[] repos);
        string GetStalePRsQuery(params RepoDefinition[] repos);
    }
}
