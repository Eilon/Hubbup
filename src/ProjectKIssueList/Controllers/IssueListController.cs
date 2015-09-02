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
        private static readonly Dictionary<string, string[]> RepoSets = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "kcore",
                new string[] {
                    "aspnet-docker",
                    "BasicMiddleware",
                    "Caching",
                    //"CoreCLR",
                    "DataProtection",
                    "dnvm",
                    "dnx",
                    "Entropy",
                    "FileSystem",
                    //"Helios",
                    "homebrew-dnx",
                    "Hosting",
                    "HttpAbstractions",
                    "HttpClient",
                    "KestrelHttpServer",
                    "Logging",
                    "Proxy",
                    "ResponseCaching",
                    //"Roslyn",
                    "Security",
                    "ServerTests",
                    "Session",
                    //"Setup",
                    "SignalR-Client-Cpp",
                    "SignalR-Client-Java",
                    "SignalR-Client-JS",
                    "SignalR-Client-Net",
                    "SignalR-Redis",
                    "SignalR-Server",
                    "SignalR-ServiceBus",
                    "SignalR-SqlServer",
                    "Signing",
                    "StaticFiles",
                    "UserSecrets",
                    //"WebListener",
                    "WebSockets",
                }
            },
            {
                "mvc",
                new string[] {
                    "Antiforgery",
                    "Common",
                    "CORS",
                    "DependencyInjection",
                    "Diagnostics",
                    "EventNotification",
                    "jquery-ajax-unobtrusive",
                    "jquery-validation-unobtrusive",
                    "Localization",
                    "MusicStore",
                    "Mvc",
                    "Razor",
                    //"RazorTooling",
                    "Routing",
                }
            },
        };

        private Task<IReadOnlyList<Issue>> GetIssuesForRepo(string repo, string gitHubAccessToken)
        {
            // TODO: Change TestApp name

            var ghc = new GitHubClient(
                new ProductHeaderValue("TestApp"),
                new InMemoryCredentialStore(new Credentials(gitHubAccessToken)));

            var repositoryIssueRequest = new RepositoryIssueRequest
            {
                State = ItemState.Open,
            };
            repositoryIssueRequest.Labels.Add("2 - Working");

            return ghc.Issue.GetAllForRepository("aspnet", repo, repositoryIssueRequest);
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
                RepoSets.ContainsKey(repoSet ?? string.Empty)
                ? RepoSets[repoSet]
                : RepoSets.SelectMany(x => x.Value);

            var allIssuesByRepo = new ConcurrentDictionary<string, Task<IReadOnlyList<Issue>>>();

            Parallel.ForEach(repos, repo => allIssuesByRepo[repo] = GetIssuesForRepo(repo, gitHubAccessToken));

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
                            .GroupBy(issue => issue.Issue.Assignee.Login)
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
                            .GroupBy(issue => issue.Issue.Milestone.Title)
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
