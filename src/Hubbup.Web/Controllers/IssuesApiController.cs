using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hubbup.Web.DataSources;
using Hubbup.Web.Models;
using Hubbup.Web.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Hubbup.Web.Controllers
{
    [Route("api")]
    [Authorize(AuthenticationSchemes = "Cookies")]
    public class IssuesApiController : Controller
    {
        private readonly IDataSource _dataSource;
        private readonly IGitHubDataSource _github;
        private readonly ILogger<IssuesApiController> _logger;

        public IssuesApiController(IDataSource dataSource, IGitHubDataSource github, ILogger<IssuesApiController> logger)
        {
            _dataSource = dataSource;
            _github = github;
            _logger = logger;
        }

        [Route("repoSets/{repoSetName}/people")]
        public IActionResult GetPeopleInRepoSet(string repoSetName)
        {
            var personSetName = _dataSource.GetRepoDataSet().GetRepoSet(repoSetName).AssociatedPersonSetName;
            var personSet = _dataSource.GetPersonSet(personSetName);
            if (personSet == null)
            {
                return NotFound();
            }
            return Json(personSet.People);
        }

        [Route("repoSets/{repoSetName}/issues/{userName}")]
        public async Task<IActionResult> GetIssuesByUserAsync(string repoSetName, string userName)
        {
            var repoSet = _dataSource.GetRepoDataSet().GetRepoSet(repoSetName);
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            // Issue the three queries simultaneously and wait for results
            var assignedIssuesQuery = repoSet.GenerateQuery("is:open", "is:issue", $"assignee:{userName}");
            var assignedPrsQuery = repoSet.GenerateQuery("is:open", "is:pr", $"assignee:{userName}");
            var createdPrsQuery = repoSet.GenerateQuery("is:open", "is:pr", $"author:{userName}");
            var assignedIssuesTask = _github.SearchIssuesAsync(assignedIssuesQuery, accessToken);
            var assignedPrsTask = _github.SearchIssuesAsync(assignedPrsQuery, accessToken);
            var createdPrsTask = _github.SearchIssuesAsync(createdPrsQuery, accessToken);
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
            _logger.LogDebug("Fetched issues for {user} in repo group {group}. Total Rate Limit Cost: {cost}", userName, repoSetName, rateLimitCost.Cost);

            return Json(new
            {
                working = SortWorkingIssues(workingIssues),
                other = SortOtherAssignedIssues(otherIssues),
                prs = SortPRs(Enumerable.Concat(assignedPrs.Search, createdPrs.Search)),
                graphQlRateLimit = rateLimitCost,
                restRateLimit = gitHub.GetLastApiInfo()?.RateLimit,
                pages = assignedIssues.Pages + assignedPrs.Pages + createdPrs.Pages,
                queries = new string[]
                {
                    assignedIssuesQuery,
                    assignedPrsQuery,
                    createdPrsQuery
                }
            });
        }

        private IReadOnlyList<IssueData> SortWorkingIssues(IEnumerable<IssueData> issues)
        {
            var x = issues.ToList();
            var y = issues
                .OrderBy(i => i.WorkingStartedAt)
                .ThenBy(i => i.Number)
                .Distinct(IssueComparer.Instance)
                .ToList();
            return y;
        }

        private IReadOnlyList<IssueData> SortOtherAssignedIssues(IEnumerable<IssueData> issues)
        {
            var x = issues.ToList();
            var y = issues
                .OrderBy(i => i.Repository.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Number)
                .Distinct(IssueComparer.Instance)
                .ToList();
            return y;
        }

        private IReadOnlyList<IssueData> SortPRs(IEnumerable<IssueData> prs)
        {
            return prs
                .OrderBy(i => i.CreatedAt)
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
