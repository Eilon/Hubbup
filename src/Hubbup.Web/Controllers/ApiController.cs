using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hubbup.Web.DataSources;
using Hubbup.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hubbup.Web.Controllers
{
    [Route("api")]
    public class ApiController : Controller
    {
        private readonly IDataSource _dataSource;
        private readonly IGitHubDataSource _github;
        private readonly ILogger<ApiController> _logger;

        public ApiController(IDataSource dataSource, IGitHubDataSource github, ILogger<ApiController> logger)
        {
            _dataSource = dataSource;
            _github = github;
            _logger = logger;
        }

        [Route("groups/{groupName}/issues/{userName}")]
        public async Task<IActionResult> GetIssuesAsync(string groupName, string userName)
        {
            var repoSet = _dataSource.GetRepoDataSet().GetRepoSet(groupName);

            // Issue the three queries simultaneously and wait for results
            var assignedIssuesTask = _github.SearchIssuesAsync(repoSet.BaseQuery + $" is:issue assignee:{userName}", await HttpContext.GetTokenAsync("access_token"));
            var assignedPrsTask = _github.SearchIssuesAsync(repoSet.BaseQuery + $" is:pr assignee:{userName}", await HttpContext.GetTokenAsync("access_token"));
            var createdPrsTask = _github.SearchIssuesAsync(repoSet.BaseQuery + $" is:pr author:{userName}", await HttpContext.GetTokenAsync("access_token"));
            await Task.WhenAll(assignedIssuesTask, assignedPrsTask, createdPrsTask);
            var assignedIssues = await assignedIssuesTask;
            var assignedPrs = await assignedPrsTask;
            var createdPrs = await createdPrsTask;

            // Identify issues being worked on
            var workingIssues = new List<IssueData>();
            var otherIssues = new List<IssueData>();
            foreach (var result in assignedIssues.Search)
            {
                if (result.Labels.Any(l => repoSet.WorkingLabels.Contains(l.Name)))
                {
                    workingIssues.Add(result);
                }
                else
                {
                    otherIssues.Add(result);
                }
            }

            IReadOnlyList<IssueData> SortIssues(IEnumerable<IssueData> issues) =>
                issues.OrderBy(i => i.Repository.Owner.Name).ThenBy(i => i.Repository.Name).ThenBy(i => i.Number).ToList();

            // Update rate limit information
            var rateLimitCost = RateLimitInfo.Add(RateLimitInfo.Add(assignedIssues.RateLimit, assignedPrs.RateLimit), createdPrs.RateLimit);
            _logger.LogInformation("Fetched issues for {user} in repo group {group}. Total Rate Limit Cost: {cost}", userName, groupName, rateLimitCost.Cost);

            return Json(new
            {
                working = SortIssues(workingIssues),
                other = SortIssues(otherIssues),
                prs = SortIssues(Enumerable.Concat(assignedPrs.Search, createdPrs.Search)),
                rateLimit = rateLimitCost
            });
        }
    }
}
