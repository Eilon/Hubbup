using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hubbup.Web.DataSources;
using Hubbup.Web.Models;
using Hubbup.Web.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Octokit;

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
        public async Task<IActionResult> GetIssuesByUserAsync(string groupName, string userName)
        {
            var repoSet = _dataSource.GetRepoDataSet().GetRepoSet(groupName);
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            // Issue the three queries simultaneously and wait for results
            var assignedIssuesTask = _github.SearchIssuesAsync(repoSet.GenerateQuery("is:open", "is:issue", $"assignee:{userName}"), accessToken);
            var assignedPrsTask = _github.SearchIssuesAsync(repoSet.GenerateQuery("is:open", "is:pr", $"assignee:{userName}"), accessToken);
            var createdPrsTask = _github.SearchIssuesAsync(repoSet.GenerateQuery("is:open", "is:pr", $"author:{userName}"), accessToken);
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
                    // We need to grab additional data about Working issues
                    result.Working = true;
                    result.WorkingStartedAt = await GetWorkingStartTime(result, repoSet.WorkingLabels, gitHub);

                    workingIssues.Add(result);
                }
                else
                {
                    otherIssues.Add(result);
                }
            }

            // Update rate limit information
            var rateLimitCost = RateLimitInfo.Add(RateLimitInfo.Add(assignedIssues.RateLimit, assignedPrs.RateLimit), createdPrs.RateLimit);
            _logger.LogInformation("Fetched issues for {user} in repo group {group}. Total Rate Limit Cost: {cost}", userName, groupName, rateLimitCost.Cost);

            return Json(new
            {
                working = SortIssues(workingIssues),
                other = SortIssues(otherIssues),
                prs = SortIssues(Enumerable.Concat(assignedPrs.Search, createdPrs.Search)),
                graphQlRateLimit = rateLimitCost,
                restRateLimit = gitHub.GetLastApiInfo()?.RateLimit,
                pages = assignedIssues.Pages + assignedPrs.Pages + createdPrs.Pages
            });
        }

        private IReadOnlyList<IssueData> SortIssues(IEnumerable<IssueData> issues)
        {
            return issues
                .OrderBy(i => i.Repository.Owner.Name)
                .ThenBy(i => i.Repository.Name)
                .ThenBy(i => i.Number)
                .Distinct(IssueComparer.Instance)
                .ToList();
        }

        private static async Task<DateTimeOffset?> GetWorkingStartTime(IssueData issue, HashSet<string> workingLabels, IGitHubClient gitHubClient)
        {
            // No GraphQL API for this :(
            var workingLabelsOnThisIssue =
                issue.Labels
                    .Where(label => workingLabels.Contains(label.Name, StringComparer.OrdinalIgnoreCase))
                    .Select(label => label.Name);

            if (!workingLabelsOnThisIssue.Any())
            {
                // Item isn't in any Working state, so ignore it
                return null;
            }

            // Find all "labeled" events for this issue
            var issueEvents = await gitHubClient.Issue.Events.GetAllForIssue(issue.Repository.Owner.Login, issue.Repository.Name, issue.Number);
            var lastApiInfo = gitHubClient.GetLastApiInfo();

            foreach (var workingLabelOnThisIssue in workingLabelsOnThisIssue)
            {
                var labelEvent = issueEvents.LastOrDefault(
                    issueEvent =>
                        issueEvent.Event == EventInfoState.Labeled &&
                        string.Equals(issueEvent.Label.Name, workingLabelOnThisIssue, StringComparison.OrdinalIgnoreCase));

                if (labelEvent != null)
                {
                    // If an event where this label was applied was found, return the date on which it was applied
                    return labelEvent.CreatedAt;
                }
            }

            return null;
        }

        private class IssueComparer : IEqualityComparer<IssueData>
        {
            public static readonly IssueComparer Instance = new IssueComparer();

            private IssueComparer()
            {

            }

            public bool Equals(IssueData x, IssueData y)
            {
                return string.Equals(x.Repository.Id, y.Repository.Id) &&
                    x.Number == y.Number;
            }

            public int GetHashCode(IssueData obj)
            {
                return obj.Id.GetHashCode();
            }
        }
    }
}
