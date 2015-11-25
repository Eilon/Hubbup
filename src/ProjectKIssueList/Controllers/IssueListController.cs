using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.WebEncoders;
using NuGet;
using Octokit;
using ProjectKIssueList.Models;
using ProjectKIssueList.Utils;
using ProjectKIssueList.ViewModels;

namespace ProjectKIssueList.Controllers
{
    public class IssueListController : Controller
    {
        public IssueListController(IRepoSetProvider repoSetProvider, IPersonSetProvider personSetProvider, IUrlEncoder urlEncoder)
        {
            RepoSetProvider = repoSetProvider;
            PersonSetProvider = personSetProvider;
            UrlEncoder = urlEncoder;
        }

        public IRepoSetProvider RepoSetProvider { get; }

        public IPersonSetProvider PersonSetProvider { get; private set; }

        public IUrlEncoder UrlEncoder { get; }

        private RepoTask<IReadOnlyList<Issue>> GetIssuesForRepo(string owner, string repo, GitHubClient gitHubClient)
        {
            var repositoryIssueRequest = new RepositoryIssueRequest
            {
                State = ItemState.Open,
            };

            return new RepoTask<IReadOnlyList<Issue>>
            {
                Repo = new RepoDefinition(owner, repo),
                Task = gitHubClient.Issue.GetAllForRepository(owner, repo, repositoryIssueRequest),
            };
        }

        private RepoTask<IReadOnlyList<PullRequest>> GetPullRequestsForRepo(string owner, string repo, GitHubClient gitHubClient)
        {
            return new RepoTask<IReadOnlyList<PullRequest>>
            {
                Repo = new RepoDefinition(owner, repo),
                Task = gitHubClient.PullRequest.GetAllForRepository(owner, repo),
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

        private static async Task<DateTimeOffset?> GetWorkingStartTime(RepoDefinition repo, Issue issue, string workingLabel, GitHubClient gitHubClient)
        {
            if (!issue.Labels.Any(label => string.Equals(label.Name, workingLabel, StringComparison.OrdinalIgnoreCase)))
            {
                // Item isn't in Working state, so ignore it
                return null;
            }

            // Find all "labeled" events for this issue
            var issueEvents = await gitHubClient.Issue.Events.GetAllForIssue(repo.Owner, repo.Name, issue.Number);
            var labelEvent = issueEvents.LastOrDefault(issueEvent => issueEvent.Event == EventInfoState.Labeled && string.Equals(issueEvent.Label.Name, workingLabel, StringComparison.OrdinalIgnoreCase));
            if (labelEvent == null)
            {
                // Couldn't find a "labeled" event where the Working label was added - probably a missing GitHub event?
                return null;
            }
            return labelEvent.CreatedAt;
        }

        [Route("{repoSet}")]
        [GitHubAuthData]
        public IActionResult Index(string repoSet, string gitHubAccessToken, string gitHubName)
        {
            // Authenticated and all claims have been read

            if (!RepoSetProvider.RepoSetExists(repoSet))
            {
                return HttpNotFound();
            }

            var repos = RepoSetProvider.GetRepoSet(repoSet);
            var personSetName = repos.AssociatedPersonSetName;
            var personSet = PersonSetProvider.GetPersonSet(personSetName);
            var peopleInPersonSet = personSet?.People ?? new string[0];

            var allIssuesByRepo = new ConcurrentDictionary<RepoDefinition, RepoTask<IReadOnlyList<Issue>>>();
            var allPullRequestsByRepo = new ConcurrentDictionary<RepoDefinition, RepoTask<IReadOnlyList<PullRequest>>>();

            var gitHubClient = GitHubUtils.GetGitHubClient(gitHubAccessToken);

            Parallel.ForEach(repos.Repos, repo => allIssuesByRepo[repo] = GetIssuesForRepo(repo.Owner, repo.Name, gitHubClient));
            Parallel.ForEach(repos.Repos, repo => allPullRequestsByRepo[repo] = GetPullRequestsForRepo(repo.Owner, repo.Name, gitHubClient));

            // while waiting for queries to run, do some other work...

            var labelQuery = GetLabelQuery(repos.LabelFilter);

            var openIssuesQuery = GetOpenIssuesQuery(GetExcludedMilestonesQuery(), labelQuery, repos.Repos);
            var workingIssuesQuery = GetWorkingIssuesQuery(labelQuery, repos.WorkingLabel, repos.Repos);
            var unassignedIssuesQuery = GetUnassignedIssuesQuery(GetExcludedMilestonesQuery(), labelQuery, repos.Repos);
            var untriagedIssuesQuery = GetUntriagedIssuesQuery(labelQuery, repos.Repos);
            var openPRsQuery = GetOpenPRsQuery(repos.Repos);
            var stalePRsQuery = GetStalePRsQuery(repos.Repos);

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
                    .Where(repoTask => repoTask.Value.Task.IsFaulted)
                    .Select(repoTask =>
                        new RepoFailure
                        {
                            Repo = repoTask.Key,
                            FailureMessage = string.Format("Issues couldn't be retrieved for the {0}/{1} repo", repoTask.Key.Owner, repoTask.Key.Name),
                        }));
            repoFailures.AddRange(
                allPullRequestsByRepo
                    .Where(repoTask => repoTask.Value.Task.IsFaulted)
                    .Select(repoTask =>
                        new RepoFailure
                        {
                            Repo = repoTask.Key,
                            FailureMessage = string.Format("Pull requests couldn't be retrieved for the {0}/{1} repo", repoTask.Key.Owner, repoTask.Key.Name),
                        }));

            var allIssues = allIssuesByRepo
                .Where(repoTask => !repoTask.Value.Task.IsFaulted)
                .SelectMany(issueList =>
                    issueList.Value.Task.Result
                        .Where(
                            issue =>
                                !IsExcludedMilestone(issue.Milestone?.Title) &&
                                issue.PullRequest == null &&
                                IsFilteredIssue(issue, repos))
                        .Select(
                            issue => new IssueWithRepo
                            {
                                Issue = issue,
                                Repo = issueList.Key,
                                WorkingStartTime = GetWorkingStartTime(issueList.Key, issue, repos.WorkingLabel, gitHubClient).Result,
                                IsInAssociatedPersonSet = IsInAssociatedPersonSet(issue.Assignee?.Login, personSet),
                            }))
                .OrderBy(issueWithRepo => issueWithRepo.WorkingStartTime)
                .ToList();

            var workingIssues = allIssues
                .Where(issue => issue.Issue.Labels.Any(label => string.Equals(label.Name, repos.WorkingLabel, StringComparison.OrdinalIgnoreCase))).ToList();

            var untriagedIssues = allIssues
                .Where(issue => issue.Issue.Milestone == null).ToList();

            var unassignedIssues = allIssues
                .Where(issue => issue.Issue.Assignee == null).ToList();

            var allPullRequests = allPullRequestsByRepo
                .Where(repoTask => !repoTask.Value.Task.IsFaulted)
                .SelectMany(pullRequestList =>
                    pullRequestList.Value.Task.Result
                        .Select(pullRequest =>
                            new PullRequestWithRepo
                            {
                                PullRequest = pullRequest,
                                Repo = pullRequestList.Key,
                                IsInAssociatedPersonSet = IsInAssociatedPersonSet(pullRequest.User?.Login, personSet),
                            }))
                .OrderBy(pullRequestWithRepo => pullRequestWithRepo.PullRequest.CreatedAt)
                .ToList();


            var milestoneData = repos.Repos
                .Select(repo =>
                    new MilestoneSummary()
                    {
                        Repo = repo,
                        MilestoneData = allIssues
                            .Where(issue => issue.Repo == repo)
                            .GroupBy(issue => issue.Issue.Milestone?.Title)
                            .Select(issueMilestoneGroup => new MilestoneData
                            {
                                Milestone = issueMilestoneGroup.Key,
                                OpenIssues = issueMilestoneGroup.Count(),
                                // TODO: Need to add PullRequest.Milestone to Octokit
                                //OpenPRs = allPullRequests.Where(pr => pr.Repo == repo && pr.PullRequest.Milestone?.Title == issueMilestoneGroup.Key),
                            })
                            .ToList(),
                    });
            var fullSortedMilestoneList = milestoneData
                .SelectMany(milestone => milestone.MilestoneData)
                .Select(milestone => milestone.Milestone)
                .Distinct()
                .OrderBy(milestone => new PossibleSemanticVersion(milestone));

            return View(new IssueListViewModel
            {
                RepoFailures = repoFailures,

                GitHubUserName = gitHubName,
                LastUpdated = DateTimeOffset.Now.ToPacificTime().ToString(),

                RepoSetName = repoSet,
                RepoSetNames = RepoSetProvider.GetRepoSetLists().Select(repoSetList => repoSetList.Key).ToArray(),

                TotalIssues = allIssues.Count,
                WorkingIssues = workingIssues.Count,
                UntriagedIssues = untriagedIssues.Count,
                UnassignedIssues = unassignedIssues.Count,

                ReposIncluded = repos.Repos
                    .OrderBy(repo => repo.Owner.ToLowerInvariant())
                    .ThenBy(repo => repo.Name.ToLowerInvariant())
                    .Select(repo => new RepoSummary
                    {
                        Repo = repo,
                        OpenIssues = allIssues.Where(issue => issue.Repo == repo).Count(),
                        OpenIssuesQueryUrl = GetOpenIssuesQuery(GetExcludedMilestonesQuery(), labelQuery, repo),
                        UnassignedIssues = allIssues.Where(issue => issue.Repo == repo && issue.Issue.Assignee == null).Count(),
                        UnassignedIssuesQueryUrl = GetUnassignedIssuesQuery(GetExcludedMilestonesQuery(), labelQuery, repo),
                        UntriagedIssues = allIssues.Where(issue => issue.Repo == repo && issue.Issue.Milestone == null).Count(),
                        UntriagedIssuesQueryUrl = GetUntriagedIssuesQuery(labelQuery, repo),
                        WorkingIssues = allIssues.Where(issue => issue.Repo == repo && workingIssues.Contains(issue)).Count(),
                        WorkingIssuesQueryUrl = GetWorkingIssuesQuery(labelQuery, repos.WorkingLabel, repo),
                        OpenPRs = allPullRequests.Where(pullRequest => pullRequest.Repo == repo).Count(),
                        OpenPRsQueryUrl = GetOpenPRsQuery(repo),
                        StalePRs = allPullRequests.Where(pullRequest => pullRequest.Repo == repo && pullRequest.PullRequest.CreatedAt < DateTimeOffset.Now.AddDays(-14)).Count(),
                        StalePRsQueryUrl = GetStalePRsQuery(repo),
                    })
                    .ToList(),

                MilestoneSummary = milestoneData.ToList(),
                MilestonesAvailable = fullSortedMilestoneList.ToList(),

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
                            .Concat(
                                allIssues
                                    .Select(issueWithRepo => issueWithRepo.Issue.Assignee?.Login)
                                    .Except(peopleInPersonSet, StringComparer.OrdinalIgnoreCase)
                                    .OrderBy(person => person, StringComparer.OrdinalIgnoreCase))
                            .Distinct()
                            .Select(person =>
                                new GroupByAssigneeAssignee
                                {
                                    Assignee = person,
                                    IsInAssociatedPersonSet = IsInAssociatedPersonSet(person, personSet),
                                    Issues = workingIssues
                                        .Where(workingIssue =>
                                            workingIssue.Issue.Assignee?.Login == person)
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
                            .ToList()
                            .AsReadOnly()
                },

                GroupByMilestone = new GroupByMilestoneViewModel
                {
                    Milestones =
                        workingIssues
                            .GroupBy(issue => issue.Issue.Milestone?.Title)
                            .Select(group =>
                                new GroupByMilestoneMilestone
                                {
                                    Milestone = group.Key,
                                    Issues = group.ToList().AsReadOnly(),
                                })
                            .OrderBy(group => group.Milestone, StringComparer.OrdinalIgnoreCase)
                            .ToList()
                            .AsReadOnly()
                },

                GroupByRepo = new GroupByRepoViewModel
                {
                    Repos =
                        workingIssues
                            .GroupBy(issue => issue.Repo)
                            .Select(group =>
                                new GroupByRepoRepo
                                {
                                    Repo = group.Key,
                                    Issues = group.ToList().AsReadOnly(),
                                })
                            .OrderByDescending(group => group.Issues.Count)
                            .ToList()
                            .AsReadOnly()
                },

                PullRequests = allPullRequests,
            });
        }

        private string GetLabelQuery(string labelFilter)
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

        private string GetExcludedMilestonesQuery()
        {
            return string.Join(" ", ExcludedMilestones.Select(milestone => "-milestone:\"" + milestone + "\""));
        }

        private string GetStalePRDate()
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

            return GitHubQueryPrefix + UrlEncoder.UrlEncode(string.Join(" ", rawQueryParts)) + "&s=updated";
        }

        private string GetOpenIssuesQuery(string excludedMilestonesQuery, string labelQuery, params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:issue", "is:open", GetRepoQuery(repos), excludedMilestonesQuery, labelQuery);
        }

        private string GetWorkingIssuesQuery(string labelQuery, string workingLabel, params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:issue", "is:open", $"label:\"{workingLabel}\"", GetRepoQuery(repos), labelQuery);
        }

        private string GetUntriagedIssuesQuery(string labelQuery, params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:issue", "is:open", "no:milestone", GetRepoQuery(repos), labelQuery);
        }

        private string GetUnassignedIssuesQuery(string excludedMilestonesQuery, string labelQuery, params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:issue", "is:open", "no:assignee", GetRepoQuery(repos), excludedMilestonesQuery, labelQuery);
        }

        private string GetOpenPRsQuery(params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:pr", "is:open", GetRepoQuery(repos));
        }

        private string GetStalePRsQuery(params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:pr", "is:open", "created:<=" + GetStalePRDate(), GetRepoQuery(repos));
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
                SemanticVersion semanticVersion;
                if (SemanticVersion.TryParse(possibleSemanticVersion, out semanticVersion))
                {
                    SemanticVersion = semanticVersion;
                }
                else
                {
                    NonSemanticVersion = possibleSemanticVersion;
                }
            }

            public string NonSemanticVersion { get; }

            public SemanticVersion SemanticVersion { get; }

            public int CompareTo(PossibleSemanticVersion other)
            {
                if (other == null)
                {
                    return 1;
                }

                if (SemanticVersion != null)
                {
                    if (other.SemanticVersion != null)
                    {
                        return SemanticVersion.CompareTo(other.SemanticVersion);
                    }
                    else
                    {
                        return 1;
                    }
                }
                else
                {
                    if (other.SemanticVersion != null)
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
}
