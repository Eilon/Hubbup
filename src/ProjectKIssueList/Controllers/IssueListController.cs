using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Authentication;
using Microsoft.AspNet.Mvc;
using Octokit;
using Octokit.Internal;
using ProjectKIssueList.Models;

namespace ProjectKIssueList.Controllers
{
    public class IssueListController : Controller
    {
        private GitHubClient GetGitHubClient(string gitHubAccessToken)
        {
            var ghc = new GitHubClient(
                new ProductHeaderValue("Project-K-Issue-List"),
                new InMemoryCredentialStore(new Credentials(gitHubAccessToken)));

            return ghc;
        }

        private Task<IReadOnlyList<Issue>> GetIssuesForRepo(string repo, GitHubClient gitHubClient)
        {
            var repositoryIssueRequest = new RepositoryIssueRequest
            {
                State = ItemState.Open,
            };
            repositoryIssueRequest.Labels.Add("2 - Working");

            return gitHubClient.Issue.GetAllForRepository("aspnet", repo, repositoryIssueRequest);
        }

        private Task<IReadOnlyList<PullRequest>> GetPullRequestsForRepo(string repo, GitHubClient gitHubClient)
        {
            return gitHubClient.PullRequest.GetAllForRepository("aspnet", repo);
        }

        [Route("{repoSet?}")]
        public IActionResult Index(string repoSet)
        {
            var gitHubAccessToken = Context.Session.GetString("GitHubAccessToken");
            var gitHubName = Context.Session.GetString("GitHubName");

            // If session state didn't have our data, either there's no one logged in, or they just logged in
            // but the claims haven't yet been read.
            if (string.IsNullOrEmpty(gitHubAccessToken))
            {
                if (!User.Identity.IsAuthenticated)
                {
                    // Not authenticated at all? Go to GitHub to authorize the app
                    return new ChallengeResult("GitHub", new AuthenticationProperties { RedirectUri = "/" + repoSet });
                }

                // Authenticated but haven't read the claims? Process the claims
                gitHubAccessToken = Context.User.FindFirst("access_token")?.Value;
                gitHubName = Context.User.Identity.Name;
                Context.Session.SetString("GitHubAccessToken", gitHubAccessToken);
                Context.Session.SetString("GitHubName", gitHubName);
            }

            // Authenticated and all claims have been read

            var repos =
                RepoSets.HasRepoSet(repoSet ?? string.Empty)
                ? RepoSets.GetRepoSet(repoSet)
                : RepoSets.GetAllRepos();

            var allIssuesByRepo = new ConcurrentDictionary<string, Task<IReadOnlyList<Issue>>>();

            Parallel.ForEach(repos, repo => allIssuesByRepo[repo] = GetIssuesForRepo(repo, GetGitHubClient(gitHubAccessToken)));

            Task.WaitAll(allIssuesByRepo.Select(x => x.Value).ToArray());

            var allIssues = allIssuesByRepo.SelectMany(issueList => issueList.Value.Result.Select(issue => new IssueWithRepo
            {
                Issue = issue,
                RepoName = issueList.Key,
            })).ToList();

            return View(new HomeViewModel
            {
                TotalIssues = allIssues.Count,

                Name = gitHubName,

                GroupByAssignee = new GroupByAssigneeViewModel
                {
                    Assignees =
                        allIssues
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
                        allIssues
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
                        allIssues
                            .GroupBy(issue => issue.RepoName)
                            .Select(group =>
                                new GroupByRepoRepo
                                {
                                    RepoName = group.Key,
                                    Issues = group.ToList().AsReadOnly(),
                                })
                            .OrderByDescending(group => group.Issues.Count)
                            .ToList()
                            .AsReadOnly()
                }
            });
        }
    }
}
