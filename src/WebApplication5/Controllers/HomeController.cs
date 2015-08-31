using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Octokit;
using System.Collections.Concurrent;
using Octokit.Internal;
using Microsoft.Framework.Configuration;
using WebApplication5.Models;

namespace WebApplication5.Controllers
{
    public class HomeController : Controller
    {
        private static readonly string[] Repos = new[]
        {
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
            "Mvc",
            "Razor",
        };

        public IConfiguration Configuration { get; private set; }

        public HomeController(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private Task<IReadOnlyList<Issue>> GetIssuesForRepo(string repo)
        {
            // TODO: Change TestApp name

            // TODO: Use GitHub app authorization instead: http://haacked.com/archive/2014/04/24/octokit-oauth/

            var ghc = new GitHubClient(
                new ProductHeaderValue("TestApp"),
                new InMemoryCredentialStore(new Credentials(Configuration["GitHubAuthToken"])));

            var repositoryIssueRequest = new RepositoryIssueRequest
            {
                State = ItemState.Open,
            };
            repositoryIssueRequest.Labels.Add("2 - Working");

            return ghc.Issue.GetAllForRepository("aspnet", repo, repositoryIssueRequest);
        }

        public IActionResult Index()
        {
            var allIssuesByRepo = new ConcurrentDictionary<string, Task<IReadOnlyList<Issue>>>();

            Parallel.ForEach(Repos, repo => allIssuesByRepo[repo] = GetIssuesForRepo(repo));

            Task.WaitAll(allIssuesByRepo.Select(x => x.Value).ToArray());

            var allIssues = allIssuesByRepo.SelectMany(issueList => issueList.Value.Result.Select(issue => new IssueWithRepo
            {
                Issue = issue,
                RepoName = issueList.Key,
            })).ToList();

            // TODO: Get list of milestones
            // TODO: Client UI to group by person, by repo, or by milestone

            return View(new HomeViewModel
            {
                TotalIssues = allIssues.Count,

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

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View("~/Views/Shared/Error.cshtml");
        }
    }
}
