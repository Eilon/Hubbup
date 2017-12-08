using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Hubbup.Web.DataSources;
using Hubbup.Web.Diagnostics.Metrics;
using Hubbup.Web.Models;
using Hubbup.Web.Utils;
using Hubbup.Web.ViewModels;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using Octokit;

namespace Hubbup.Web.Controllers
{
    public class TriageController : Controller, IGitHubQueryProvider
    {
        private static readonly string[] ExcludedMilestones = new[] {
            "Backlog",
            "Discussion",
            "Discussions",
            "Future",
        };

        private readonly IDataSource _dataSource;
        private readonly UrlEncoder _urlEncoder;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<TriageController> _logger;

        public TriageController(
            IDataSource dataSource,
            UrlEncoder urlEncoder,
            IMetricsService metricsService,
            ILogger<TriageController> logger)
        {
            _dataSource = dataSource;
            _urlEncoder = urlEncoder;
            _metricsService = metricsService;
            _logger = logger;
        }

        private RepoTask<IReadOnlyList<Issue>> GetIssuesForRepo(RepoDefinition repo, IGitHubClient gitHubClient, string metricsPrefix)
        {
            async Task<IReadOnlyList<Issue>> RunRequestAsync()
            {
                var repositoryIssueRequest = new RepositoryIssueRequest
                {
                    State = ItemStateFilter.Open,
                };

                using (_metricsService.Time($"{metricsPrefix}:Repo({repo.Owner}/{repo.Name}):GetAllIssues"))
                {
                    return await gitHubClient.Issue.GetAllForRepository(repo.Owner, repo.Name, repositoryIssueRequest);
                }
            }

            return new RepoTask<IReadOnlyList<Issue>>
            {
                Repo = repo,
                Task = RunRequestAsync(),
            };
        }

        private RepoTask<IReadOnlyList<PullRequest>> GetPullRequestsForRepo(RepoDefinition repo, IGitHubClient gitHubClient, string metricsPrefix)
        {
            async Task<IReadOnlyList<PullRequest>> RunRequestAsync()
            {
                using (_metricsService.Time($"{metricsPrefix}:Repo({repo.Owner}/{repo.Name}):GetAllPullRequests"))
                {
                    return await gitHubClient.PullRequest.GetAllForRepository(repo.Owner, repo.Name);
                }
            }

            return new RepoTask<IReadOnlyList<PullRequest>>
            {
                Repo = repo,
                Task = RunRequestAsync(),
            };
        }

        private static bool IsExcludedMilestone(string repoName)
        {
            return ExcludedMilestones.Contains(repoName, StringComparer.OrdinalIgnoreCase);
        }

        [Route("/triage/{repoSet}")]
        [Authorize]
        public async Task<IActionResult> Index(string repoSet)
        {
            using (_logger.BeginScope("Requesting Triage Data for {RepoSet}", repoSet))
            {
                HttpContext.AddTelemetryProperty("RepoSet", repoSet);
                HttpContext.AddTelemetryProperty("RepoSetView", "Triage");
                var metricsPrefix = $"TriageController:RepoSet({repoSet})";

                var gitHubName = HttpContext.User.Identity.Name;
                HttpContext.AddTelemetryProperty("GitHubUser", gitHubName);

                var gitHubAccessToken = await HttpContext.GetTokenAsync("access_token");
                // Authenticated and all claims have been read

                var repoDataSet = _dataSource.GetRepoDataSet();

                if (!repoDataSet.RepoSetExists(repoSet))
                {
                    var invalidRepoSetPageViewTelemetry = new PageViewTelemetry("RepoSet")
                    {
                        Url = new Uri(Request.GetDisplayUrl()),
                    };
                    HttpContext.AddTelemetryProperty("RepoSetValid", false);
                    return NotFound();
                }

                var requestStopwatch = new Stopwatch();
                requestStopwatch.Start();

                var repos = repoDataSet.GetRepoSet(repoSet);
                var distinctRepos =
                    repos.Repos
                        .Distinct()
                        .Where(repo => repo.RepoInclusionLevel != RepoInclusionLevel.None)
                        .ToArray();
                var personSetName = repos.AssociatedPersonSetName;
                var personSet = _dataSource.GetPersonSet(personSetName);
                var peopleInPersonSet = personSet?.People ?? new string[0];
                var workingLabels = repos.WorkingLabels ?? new HashSet<string>();

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

                using (_metricsService.Time($"{metricsPrefix}:GetAllRepositories"))
                {
                    var getAllOrgReposTask = AsyncParallelUtils.ForEachAsync(distinctOrgs, 5, async org =>
                    {
                        IReadOnlyList<Repository> reposInOrg;
                        using (_metricsService.Time($"{metricsPrefix}:Org({org}):GetRepositories"))
                        {
                            reposInOrg = await gitHubClient.Repository.GetAllForOrg(org);
                        }
                        allOrgRepos[org] = reposInOrg.Where(repo => !repo.Fork).Select(repo => repo.Name).ToArray();
                    });
                    await getAllOrgReposTask;
                }

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
                Parallel.ForEach(distinctRepos, repo => allIssuesByRepo[repo] = GetIssuesForRepo(repo, gitHubClient, metricsPrefix));
                Parallel.ForEach(distinctRepos, repo => allPullRequestsByRepo[repo] = GetPullRequestsForRepo(repo, gitHubClient, metricsPrefix));

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

                var failuresOccurred = false;
                try
                {
                    Task.WaitAll(allIssuesByRepo.Select(x => x.Value.Task).ToArray());
                }
                catch (AggregateException)
                {
                    // Just hide the exceptions here - faulted tasks will be aggregated later
                    failuresOccurred = true;
                }

                try
                {
                    Task.WaitAll(allPullRequestsByRepo.Select(x => x.Value.Task).ToArray());
                }
                catch (AggregateException)
                {
                    // Just hide the exceptions here - faulted tasks will be aggregated later
                    failuresOccurred = true;
                }

                using (_metricsService.Time($"{metricsPrefix}:PostQueryProcessingTime"))
                {
                    // Log failures
                    var repoFailures = new List<RepoFailure>();
                    if (failuresOccurred)
                    {
                        repoFailures.AddRange(
                            allIssuesByRepo
                                .Where(repoTask => repoTask.Value.Task.IsFaulted || repoTask.Value.Task.IsCanceled)
                                .Select(repoTask =>
                                    new RepoFailure
                                    {
                                        Repo = repoTask.Key,
                                        IssueType = IssueType.Issue,
                                        FailureMessage = string.Format("Issues couldn't be retrieved for the {0}/{1} repo", repoTask.Key.Owner, repoTask.Key.Name),
                                        Exception = repoTask.Value.Task.Exception,
                                    }));
                        repoFailures.AddRange(
                            allPullRequestsByRepo
                                .Where(repoTask => repoTask.Value.Task.IsFaulted || repoTask.Value.Task.IsCanceled)
                                .Select(repoTask =>
                                    new RepoFailure
                                    {
                                        Repo = repoTask.Key,
                                        IssueType = IssueType.PullRequest,
                                        FailureMessage = string.Format("Pull requests couldn't be retrieved for the {0}/{1} repo", repoTask.Key.Owner, repoTask.Key.Name),
                                        Exception = repoTask.Value.Task.Exception,
                                    }));

                        foreach (var failure in repoFailures)
                        {
                            _logger.LogError(
                                failure.Exception,
                                "Error retrieving {IssueType} data for {RepositoryOwner}/{RepositoryName}",
                                failure.IssueType,
                                failure.Repo.Owner,
                                failure.Repo.Name);
                        }
                    }

                    var allIssues = allIssuesByRepo
                        .Where(repoTask => !repoTask.Value.Task.IsFaulted && !repoTask.Value.Task.IsCanceled && repoTask.Value.Task.Result.Any())
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

                    var lastApiInfo = gitHubClient.GetLastApiInfo();
                    _metricsService.Record("TriageController:RateLimitRemaining", lastApiInfo.RateLimit.Remaining);

                    var issueListViewModel = new IssueListViewModel
                    {
                        LastApiInfo = lastApiInfo,

                        RepoFailures = repoFailures,

                        GitHubUserName = gitHubName,
                        LastUpdated = DateTimeOffset.Now.ToPacificTime().ToString(),

                        ExtraLinks = repos.RepoExtraLinks,

                        RepoSetName = repoSet,
                        RepoSetNames = repoDataSet.GetRepoSetLists().Select(repoSetList => repoSetList.Key).ToArray(),

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
                                    new[]
                                    {
                                new GroupByAssigneeAssignee
                                {
                                    Assignee = "<assigned outside this person set>",
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
                                    }
                                    .ToList()
                                    .AsReadOnly(),
                        },
                    };

                    requestStopwatch.Stop();
                    issueListViewModel.PageRequestTime = requestStopwatch.Elapsed;

                    HttpContext.AddTelemetryProperty("RepoSetValid", true);

                    return View(issueListViewModel);
                }
            }
        }

        private bool ItemIncludedByInclusionLevel(string itemAssignee, RepoDefinition repo, IReadOnlyList<string> peopleInPersonSet)
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

            return GitHubQueryPrefix + _urlEncoder.Encode(string.Join(" ", rawQueryParts)) + "&s=updated";
        }

        public string GetOpenIssuesQuery(string excludedMilestonesQuery, string labelQuery, params RepoDefinition[] repos)
        {
            return GetGitHubQuery("is:issue", "is:open", GetRepoQuery(repos), excludedMilestonesQuery, labelQuery);
        }

        public string GetWorkingIssuesQuery(string labelQuery, HashSet<string> workingLabels, params RepoDefinition[] repos)
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
            HashSet<string> workingLabels,
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
        string GetWorkingIssuesQuery(string labelQuery, HashSet<string> workingLabels, params RepoDefinition[] repos);
        string GetOpenPRsQuery(params RepoDefinition[] repos);
        string GetStalePRsQuery(params RepoDefinition[] repos);
    }
}
