using Hubbup.Web.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Octokit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Hubbup.Web.Controllers
{
    [Route("miklabelaction")]
    [Authorize]
    public class MikLabelerController : Controller
    {
        private readonly IMemoryCache _memoryCache;


        public MikLabelerController(
            IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        private static string GetIssueHiderCacheKey(string owner, string repo, int issueNumber) =>
            $"HideIssue/{owner}/{repo}/{issueNumber.ToString(CultureInfo.InvariantCulture)}";

        [HttpPost]
        [Route("ApplyLabel/{owner}/{repo}/{issueNumber}/{repoSetName?}")]
        public async Task<IActionResult> ApplyLabel(string owner, string repo, int issueNumber, string prediction, string repoSetName)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            await ApplyLabel(gitHub, owner, repo, issueNumber, prediction);

            return RedirectToPage("/MikLabel", routeValues: new { repoSetName = repoSetName });
        }

        [HttpPost]
        [Route("ApplyLabels/{repoSetName?}")]
        public async Task<IActionResult> ApplyLabels([FromForm]List<string> applyDefault, string repoSetName)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            var tasks = new Task[applyDefault.Count];
            for (var i = 0; i < applyDefault.Count; i++)
            {
                var (owner, repo, number, prediction) = ParsePrediction(applyDefault[i]);
                tasks[i] = ApplyLabel(gitHub, owner, repo, number, prediction);
            }

            await Task.WhenAll(tasks);

            return RedirectToPage("/MikLabel", routeValues: new { repoSetName = repoSetName });
        }

        private (string owner, string repo, int number, string prediction) ParsePrediction(string input)
        {
            var slash = input.IndexOf('/');
            var octothorpe = input.IndexOf('#');
            var dash = input.IndexOf('-');

            var owner = input.Substring(0, slash);
            var repo = input.Substring(slash + 1, octothorpe - slash - 1);
            var number = int.Parse(input.Substring(octothorpe + 1, dash - octothorpe - 1));
            var prediction = input.Substring(dash + 1, input.Length - dash - 1);
            return (owner, repo, number, prediction);
        }

        private async Task ApplyLabel(IGitHubClient gitHub, string owner, string repo, int issueNumber, string prediction)
        {
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

            // Because GitHub search queries can show stale data, add a cache entry to
            // indicate this issue should be hidden for a while because it was just labeled.
            _memoryCache.Set(
                GetIssueHiderCacheKey(owner, repo, issueNumber),
                0, // no data is needed; the existence of the cache key is what counts
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                });
        }
    }
}
