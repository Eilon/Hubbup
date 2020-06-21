using Hubbup.Web.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Octokit;
using System;
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

            // Because GitHub search queries can show stale data, add a cache entry to
            // indicate this issue should be hidden for a while because it was just labeled.
            _memoryCache.Set(
                GetIssueHiderCacheKey(owner, repo, issueNumber),
                0, // no data is needed; the existence of the cache key is what counts
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                });

            return RedirectToPage("/MikLabel");
        }
    }
}
