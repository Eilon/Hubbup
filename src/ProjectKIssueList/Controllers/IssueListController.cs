using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.WebEncoders;
using Octokit;
using Octokit.Internal;
using ProjectKIssueList.Models;
using ProjectKIssueList.Utils;

namespace ProjectKIssueList.Controllers
{
    public class IssueListController : Controller
    {
        public IssueListController(IRepoSetProvider repoSetProvider, IUrlEncoder urlEncoder)
        {
            RepoSetProvider = repoSetProvider;
            UrlEncoder = urlEncoder;
        }

        public IRepoSetProvider RepoSetProvider { get; }

        public IUrlEncoder UrlEncoder { get; }

        private GitHubClient GetGitHubClient(string gitHubAccessToken)
        {
            var ghc = new GitHubClient(
                new ProductHeaderValue("Project-K-Issue-List"),
                new InMemoryCredentialStore(new Credentials(gitHubAccessToken)));

            return ghc;
        }

        private Task<IReadOnlyList<Issue>> GetIssuesForRepo(string owner, string repo, GitHubClient gitHubClient)
        {
            var repositoryIssueRequest = new RepositoryIssueRequest
            {
                State = ItemState.Open,
            };

            return gitHubClient.Issue.GetAllForRepository(owner, repo, repositoryIssueRequest);
        }

        private Task<IReadOnlyList<PullRequest>> GetPullRequestsForRepo(string owner, string repo, GitHubClient gitHubClient)
        {
            return gitHubClient.PullRequest.GetAllForRepository(owner, repo);
        }

        private static readonly string[] ExcludedMilestones = new[] {
            "Backlog",
            "Discussion",
            "Discussions",
        };

        private static bool IsExcludedMilestone(string repoName)
        {
            return ExcludedMilestones.Contains(repoName, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<DateTimeOffset?> GetWorkingStartTime(RepoDefinition repo, Issue issue, GitHubClient gitHubClient)
        {
            if (!issue.Labels.Any(label => label.Name == "2 - Working"))
            {
                // Item isn't in Working state, so ignore it
                return null;
            }

            // Find all "labeled" events for this issue
            var issueEvents = await gitHubClient.Issue.Events.GetAllForIssue(repo.Owner, repo.Name, issue.Number);
            var labelEvent = issueEvents.LastOrDefault(issueEvent => issueEvent.Event == EventInfoState.Labeled && issueEvent.Label.Name == "2 - Working");
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

            var repos =
                RepoSetProvider.RepoSetExists(repoSet ?? string.Empty)
                ? RepoSetProvider.GetRepoSet(repoSet)
                : RepoSetProvider.GetAllRepos();

            var allIssuesByRepo = new ConcurrentDictionary<RepoDefinition, Task<IReadOnlyList<Issue>>>();
            var allPullRequestsByRepo = new ConcurrentDictionary<RepoDefinition, Task<IReadOnlyList<PullRequest>>>();

            var gitHubClient = GetGitHubClient(gitHubAccessToken);

            Parallel.ForEach(repos, repo => allIssuesByRepo[repo] = GetIssuesForRepo(repo.Owner, repo.Name, gitHubClient));
            Parallel.ForEach(repos, repo => allPullRequestsByRepo[repo] = GetPullRequestsForRepo(repo.Owner, repo.Name, gitHubClient));

            // while waiting for queries to run, do some other work...

            var allReposQuery = GetRepoQuery(repos);

            var openIssuesQuery = GetGitHubQuery("is:issue is:open " + allReposQuery + " " + GetExcludedMilestonesQuery());
            var workingIssuesQuery = GetGitHubQuery("is:issue is:open label:\"2 - Working\" " + allReposQuery);
            var untriagedIssuesQuery = GetGitHubQuery("is:issue is:open no:milestone " + allReposQuery);
            var openPRsQuery = GetGitHubQuery("is:pr is:open " + allReposQuery);
            var stalePRsQuery = GetGitHubQuery("is:pr is:open created:<=" + GetStalePRDate() + " " + allReposQuery);

            // now wait for queries to finish executing

            Task.WaitAll(allIssuesByRepo.Select(x => x.Value).ToArray());
            Task.WaitAll(allPullRequestsByRepo.Select(x => x.Value).ToArray());

            var allIssues = allIssuesByRepo.SelectMany(
                issueList => issueList.Value.Result
                    .Where(issue => !IsExcludedMilestone(issue.Milestone?.Title) && issue.PullRequest == null)
                    .Select(
                        issue => new IssueWithRepo
                        {
                            Issue = issue,
                            Repo = issueList.Key,
                            WorkingStartTime = GetWorkingStartTime(issueList.Key, issue, gitHubClient).Result,
                        })).ToList();

            var workingIssues = allIssues
                .Where(issue => issue.Issue.Labels.Any(label => label.Name == "2 - Working")).ToList();

            var untriagedIssues = allIssues
                .Where(issue => issue.Issue.Milestone == null).ToList();

            var allPullRequests = allPullRequestsByRepo.SelectMany(
                pullRequestList => pullRequestList.Value.Result.Select(
                    pullRequest => new PullRequestWithRepo
                    {
                        PullRequest = pullRequest,
                        Repo = pullRequestList.Key,
                    }))
                    .OrderBy(pullRequestWithRepo => pullRequestWithRepo.PullRequest.CreatedAt)
                    .ToList();

            return View(new IssueListViewModel
            {
                GitHubUserName = gitHubName,

                RepoSetName = repoSet,
                RepoSetNames = RepoSetProvider.GetRepoSetLists().Select(repoSetList => repoSetList.Key).ToArray(),

                TotalIssues = allIssues.Count,
                WorkingIssues = workingIssues.Count,
                UntriagedIssues = untriagedIssues.Count,

                ReposIncluded = repos.OrderBy(repo => repo.Owner.ToLowerInvariant()).ThenBy(repo => repo.Name.ToLowerInvariant()).ToArray(),

                OpenIssuesQuery = openIssuesQuery,
                WorkingIssuesQuery = workingIssuesQuery,
                UntriagedIssuesQuery = untriagedIssuesQuery,
                OpenPRsQuery = openPRsQuery,
                StalePRsQuery = stalePRsQuery,

                GroupByAssignee = new GroupByAssigneeViewModel
                {
                    Assignees =
                        workingIssues
                            .GroupBy(issue => issue.Issue.Assignee?.Login)
                            .Select(group =>
                                new GroupByAssigneeAssignee
                                {
                                    Assignee = group.Key,
                                    Issues = group.ToList().AsReadOnly(),
                                })
                            .OrderBy(group => group.Assignee, StringComparer.OrdinalIgnoreCase)
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

        private string GetExcludedMilestonesQuery()
        {
            return string.Join(" ", ExcludedMilestones.Select(milestone => "-milestone:" + milestone));
        }

        private string GetStalePRDate()
        {
            var staleDays = 14;
            var stalePRDate = DateTimeOffset.UtcNow.ToPacificTime().AddDays(-staleDays);
            // GitHub uses the format 'YYYY-MM-DD'
            return stalePRDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string GetRepoQuery(RepoDefinition[] repos)
        {
            return string.Join(" ", repos.Select(repo => "repo:" + repo.Owner + "/" + repo.Name));
        }

        private string GetGitHubQuery(string rawQuery)
        {
            const string GitHubQueryPrefix = "https://github.com/search?q=";

            return GitHubQueryPrefix + UrlEncoder.UrlEncode(rawQuery) + " &s=updated";
        }
    }
}
