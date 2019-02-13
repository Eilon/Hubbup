using System;
using System.Collections.Generic;
using System.Globalization;
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
    [Authorize]
    public class DispatchApiController : Controller
    {
        private static readonly string[] ExcludedMilestones = new[] {
            "Backlog",
            "Discussion",
            "Discussions",
            "Future",
        };

        private readonly IDataSource _dataSource;
        private readonly IGitHubDataSource _github;
        private readonly ILogger<IssuesApiController> _logger;

        public DispatchApiController(IDataSource dataSource, IGitHubDataSource github, ILogger<IssuesApiController> logger)
        {
            _dataSource = dataSource;
            _github = github;
            _logger = logger;
        }

        [Route("dispatch/{ownerName}/{repoName}")]
        public async Task<IActionResult> GetIssuesToDispatch(string ownerName, string repoName)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            // Issue the three queries simultaneously and wait for results
            var allRepoIssuesTask = gitHub.Issue.GetAllForRepository(ownerName, repoName);
            var allLabelsTask = gitHub.Issue.Labels.GetAllForRepository(ownerName, repoName);
            await Task.WhenAll(allRepoIssuesTask, allLabelsTask);

            var allRepoIssues = await allRepoIssuesTask;
            var allLabels = await allLabelsTask;

            var sortedRepoLabelNames =
                allLabels
                    .Where(label => label.Name.StartsWith("repo:", StringComparison.OrdinalIgnoreCase))
                    .Select(label => label.Name)
                    .OrderBy(labelName => labelName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

            // TODO: Ignore Backlog/Discussion(s) milestones?

            // TODO: Project the issues to a simpler item that also includes action links for dispatching, etc.
            var repoRef = new RepositoryReference
            {
                Owner = new UserReference
                {
                    Login = ownerName,
                },
                Name = repoName,
            };
            var allIssuesWithoutRepoLabels =
                allRepoIssues
                    .Where(issue =>
                        issue.Labels.All(label => !sortedRepoLabelNames.Contains(label.Name, StringComparer.OrdinalIgnoreCase)) &&
                        !IsExcludedMilestone(issue.Milestone?.Title))
                    .Select(issue => GetIssueDataFromIssue(issue, repoRef))
                    .ToList();

            return Json(new
            {
                issuesWithoutRepoLabels = allIssuesWithoutRepoLabels,
                repoLabels = sortedRepoLabelNames,
            });
        }

        private static bool IsExcludedMilestone(string milestoneName)
        {
            return ExcludedMilestones.Contains(milestoneName, StringComparer.OrdinalIgnoreCase);
        }

        private static IssueData GetIssueDataFromIssue(Issue issue, RepositoryReference repo)
        {
            var issueData = new IssueData()
            {
                Id = issue.Number.ToString(CultureInfo.InvariantCulture),
                Url = issue.HtmlUrl,
                Number = issue.Number,
                Title = issue.Title,
                Author = CreateUserReferenceFromUser(issue.User),
                Milestone = issue.Milestone == null ? null : new Models.Milestone { Title = issue.Milestone.Title },
                CreatedAt = issue.CreatedAt.ToPacificTime(),
                UpdatedAt = issue.UpdatedAt?.ToPacificTime(),
                CommentCount = issue.Comments,
                Repository = repo,
            };

            // Load the assignees and labels
            foreach (var assignee in issue.Assignees)
            {
                issueData.Assignees.Add(CreateUserReferenceFromUser(assignee));
            }

            foreach (var label in issue.Labels)
            {
                issueData.Labels.Add(new Models.Label
                {
                    Name = label.Name,
                    Color = label.Color,
                    ForeColor = ColorMath.GetHexForeColorForBackColor(label.Color),
                });
            }

            return issueData;
        }

        private static UserReference CreateUserReferenceFromUser(User user)
        {
            return new UserReference
            {
                Id = user.Id.ToString(CultureInfo.InvariantCulture),
                Login = user.Login,
                Name = user.Name,
                AvatarUrl = user.AvatarUrl,
                Url = user.Url,
            };
        }

        [Route("dispatchto/{ownerName}/{repoName}/{issueNumber}/{destinationLabel}")]
        public async Task<IActionResult> DispatchIssueTo(string ownerName, string repoName, int issueNumber, string destinationLabel)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            var issueUpdate = new IssueUpdate();
            issueUpdate.AddLabel(destinationLabel);
            try
            {
                await gitHub.Issue.Update(ownerName, repoName, issueNumber, issueUpdate);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
