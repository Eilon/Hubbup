using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hubbup.MikLabelModel;
using Hubbup.Web.DataSources;
using Hubbup.Web.Utils;
using Hubbup.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Hubbup.Web.Controllers
{
    [Route("miklabel")]
    [Authorize]
    public class MikLabelerController : Controller
    {
        private readonly ILogger<MikLabelerController> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IMemoryCache _memoryCache;

        private static readonly (string owner, string repo)[] Repos = new[]
        {
            ("aspnet", "AspNetCore"),
            ("aspnet", "Extensions"),
        };


        public MikLabelerController(
            ILogger<MikLabelerController> logger,
            IWebHostEnvironment hostingEnvironment,
            IMemoryCache memoryCache)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
            _memoryCache = memoryCache;
        }

        [Route("")]
        public async Task<IActionResult> Index()
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            var predictionList = new List<LabelSuggestionViewModel>();
            var totalIssuesFound = 0;

            foreach (var (owner, repo) in Repos)
            {
                var modelPath = Path.Combine("ML", $"{owner}-{repo}-GitHubLabelerModel.zip");

                var existingAreaLabels = await GetAreaLabelsForRepo(gitHub, owner, repo);

                var excludeAllAreaLabelsQuery =
                    string.Join(
                        " ",
                        existingAreaLabels.Select(label => $"-label:\"{label.Name}\""));

                var getIssuesRequest = new SearchIssuesRequest($"{excludeAllAreaLabelsQuery} -milestone:Discussions")
                {
                    Is = new[] { IssueIsQualifier.Open },
                    Repos = new RepositoryCollection
                    {
                        { owner, repo }
                    },
                };

                var issueSearchResult = await gitHub.Search.SearchIssues(getIssuesRequest);
                totalIssuesFound += issueSearchResult.TotalCount;

                var labeler = new Labeler(Path.Combine(_hostingEnvironment.ContentRootPath, modelPath));

                foreach (var issue in issueSearchResult.Items)
                {
                    var predictions = labeler.PredictLabel(issue);

                    predictionList.Add(new LabelSuggestionViewModel
                    {
                        RepoOwner = owner,
                        RepoName = repo,
                        Issue = issue,
                        LabelScores = predictions.LabelScores.Select(ls => (ls, existingAreaLabels.Single(label => string.Equals(label.Name, ls.LabelName, StringComparison.OrdinalIgnoreCase)))).ToList()
                    });
                }
            }

            return View(new MikLabelViewModel
            {
                PredictionList = predictionList.OrderByDescending(prediction => prediction.Issue.CreatedAt).ToList(),
                TotalIssuesFound = totalIssuesFound,
            });
        }

        private async Task<List<Label>> GetAreaLabelsForRepo(IGitHubClient gitHub, string owner, string repo)
        {
            return await _memoryCache.GetOrCreateAsync(
                $"Labels/{owner}/{repo}",
                async cacheEntry =>
                {
                    cacheEntry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
                    return (await gitHub.Issue.Labels.GetAllForRepository(owner, repo))
                        .Where(label => label.Name.StartsWith("area-", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                });
        }

        [HttpPost]
        [Route("ApplyLabel/{owner}/{repo}/{issueNumber}")]
        public async Task<IActionResult> ApplyLabel(string owner, string repo, int issueNumber, string prediction)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            var issue = await gitHub.Issue.Get(owner, repo, issueNumber);

            var issueUpdate = new IssueUpdate
            {
                Milestone = issue.Milestone?.Number // Have to re-set milestone because otherwise it gets cleared out. See https://github.com/octokit/octokit.net/issues/1927
            };
            issueUpdate.AddLabel(prediction);
            // Add all existing labels to the update so that they don't get removed
            foreach (var label in issue.Labels)
            {
                issueUpdate.AddLabel(label.Name);
            }

            await gitHub.Issue.Update(owner, repo, issueNumber, issueUpdate);

            return RedirectToAction("Index");
        }
    }
}
