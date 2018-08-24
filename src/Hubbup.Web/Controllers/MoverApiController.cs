using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hubbup.IssueMover.Dto;
using Hubbup.Web.DataSources;
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
    public class MoverApiController : Controller
    {
        private readonly IDataSource _dataSource;
        private readonly IGitHubDataSource _github;
        private readonly ILogger<IssuesApiController> _logger;

        public MoverApiController(IDataSource dataSource, IGitHubDataSource github, ILogger<IssuesApiController> logger)
        {
            _dataSource = dataSource;
            _github = github;
            _logger = logger;
        }

        [Route("getmovedata/{fromOwnerName}/{fromRepoName}/{fromIssueNumber}")]
        public async Task<IActionResult> GetMoveData(string fromOwnerName, string fromRepoName, string fromIssueNumber)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            if (!int.TryParse(fromIssueNumber, out var fromIssueNumberInt))
            {
                return BadRequest();
            }

            var fromIssue = await gitHub.Issue.Get(fromOwnerName, fromRepoName, fromIssueNumberInt);

            var comments =
                (await gitHub.Issue.Comment.GetAllForIssue(fromOwnerName, fromRepoName, fromIssueNumberInt))
                    .Select(issueComment =>
                        new CommentData
                        {
                            Author = issueComment.User.Login,
                            Text = issueComment.Body,
                            Date = issueComment.CreatedAt,
                        })
                    .ToList();

            try
            {
                return Ok(
                    new IssueMoveData
                    {
                        RepoOwner = fromOwnerName,
                        RepoName = fromRepoName,
                        State = GetIssueState(fromIssue.State.Value),
                        HtmlUrl = fromIssue.HtmlUrl,
                        IsPullRequest = fromIssue.PullRequest != null,
                        Title = fromIssue.Title,
                        Number = fromIssue.Number,
                        Author = fromIssue.User.Login,
                        Body = fromIssue.Body,
                        Assignees = fromIssue.Assignees.Select(a => a.Login).ToArray(),
                        CreatedDate = fromIssue.CreatedAt,
                        Milestone = fromIssue.Milestone?.Title,
                        Labels = fromIssue.Labels.Select(l => new LabelData { Text = l.Name, Color = l.Color, }).ToList(),
                        Comments = comments,
                    });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private static IssueState GetIssueState(ItemState itemState)
        {
            if (itemState == ItemState.Open)
            {
                return IssueState.Open;
            }
            return IssueState.Closed;
        }

        [Route("getrepodata/{toOwnerName}/{toRepoName}")]
        public async Task<IActionResult> GetRepoData(string toOwnerName, string toRepoName)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            var repo = await gitHub.Repository.Get(toOwnerName, toRepoName);

            try
            {
                return Ok(
                    new RepoMoveData
                    {
                        Owner = repo.Owner?.Login,
                        Repo = repo.Name,
                        OpenIssueCount = repo.OpenIssuesCount,
                    });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("createlabels/{toOwnerName}/{toRepoName}")]
        public async Task<ActionResult<LabelCreateResult>> CreateLabels(
            string toOwnerName, string toRepoName,
            [FromBody] LabelCreateRequest labelCreateRequest)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            var destinationLabels = await gitHub.Issue.Labels.GetAllForRepository(toOwnerName, toRepoName);

            var listOfLabelsToCreate = labelCreateRequest.Labels
                .Where(labelNeeded =>
                    !destinationLabels
                        .Any(destinationLabel =>
                            string.Equals(
                                labelNeeded.Text,
                                destinationLabel.Name,
                                StringComparison.OrdinalIgnoreCase)))
                .ToList();

            try
            {
                foreach (var labelToCreate in listOfLabelsToCreate)
                {
                    await gitHub.Issue.Labels.Create(toOwnerName, toRepoName, new NewLabel(labelToCreate.Text, labelToCreate.Color));
                }

                return Ok(
                    new LabelCreateResult
                    {
                        LabelsCreated = listOfLabelsToCreate,
                    });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("createmilestone/{toOwnerName}/{toRepoName}")]
        public async Task<ActionResult<LabelCreateResult>> CreateMilestone(
            string toOwnerName, string toRepoName,
            [FromBody] MilestoneCreateRequest milestoneCreateRequest)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            var destinationMilestones = await gitHub.Issue.Milestone.GetAllForRepository(toOwnerName, toRepoName);
            if (destinationMilestones.Any(m => string.Equals(m.Title, milestoneCreateRequest.Milestone, StringComparison.OrdinalIgnoreCase)))
            {
                // Milestone already exists, so do nothing
                return Ok(new MilestoneCreateResult
                {
                    MilestoneCreated = null,
                });
            }

            try
            {
                await gitHub.Issue.Milestone.Create(toOwnerName, toRepoName, new NewMilestone(milestoneCreateRequest.Milestone));

                return Ok(
                    new MilestoneCreateResult
                    {
                        MilestoneCreated = milestoneCreateRequest.Milestone,
                    });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("moveissue/{toOwnerName}/{toRepoName}")]
        public async Task<ActionResult<IssueMoveResult>> MoveIssue(
            string toOwnerName, string toRepoName,
            [FromBody] IssueMoveRequest issueMoveRequest)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            var destinationMilestones = await gitHub.Issue.Milestone.GetAllForRepository(toOwnerName, toRepoName);

            try
            {
                // Create new issue
                var newIssueDetails = new NewIssue(issueMoveRequest.Title)
                {
                    Body = issueMoveRequest.Body,
                };
                if (issueMoveRequest.Milestone != null)
                {
                    // Set the milestone to the ID that matches the one in the destination repo, if it exists
                    var destinationMilestone = destinationMilestones.SingleOrDefault(m => string.Equals(m.Title, issueMoveRequest.Milestone, StringComparison.OrdinalIgnoreCase));
                    newIssueDetails.Milestone = destinationMilestone?.Number;
                }
                if (issueMoveRequest.Assignees != null)
                {
                    foreach (var assignee in issueMoveRequest.Assignees)
                    {
                        newIssueDetails.Assignees.Add(assignee);
                    }
                }
                if (issueMoveRequest.Labels != null)
                {
                    foreach (var label in issueMoveRequest.Labels)
                    {
                        newIssueDetails.Labels.Add(label);
                    }
                }

                var newIssueCreated = await gitHub.Issue.Create(toOwnerName, toRepoName, newIssueDetails);

                return Ok(
                    new IssueMoveResult
                    {
                        IssueNumber = newIssueCreated.Number,
                        HtmlUrl = newIssueCreated.HtmlUrl,
                    });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("movecomment/{toOwnerName}/{toRepoName}")]
        public async Task<ActionResult<CommentMoveResult>> MoveComment(
            string toOwnerName, string toRepoName,
            [FromBody] CommentMoveRequest commentMoveRequest)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            try
            {
                await gitHub.Issue.Comment.Create(toOwnerName, toRepoName, commentMoveRequest.IssueNumber, commentMoveRequest.Text);

                return Ok(
                    new CommentMoveResult
                    {
                    });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("closeissuecomment/{fromOwnerName}/{fromRepoName}")]
        public async Task<ActionResult<IssueCloseCommentResult>> CloseIssueComment(
            string fromOwnerName, string fromRepoName,
            [FromBody] IssueCloseCommentRequest closeCommentRequest)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            try
            {
                await gitHub.Issue.Comment.Create(fromOwnerName, fromRepoName, closeCommentRequest.IssueNumber, closeCommentRequest.Comment);

                return Ok(
                    new IssueCloseCommentResult
                    {
                    });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("lockissue/{fromOwnerName}/{fromRepoName}")]
        public async Task<ActionResult<IssueLockResult>> LockIssue(
            string fromOwnerName, string fromRepoName,
            [FromBody] IssueLockRequest issueLockRequest)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            try
            {
                await gitHub.Issue.Lock(fromOwnerName,
                         fromRepoName,
                         issueLockRequest.IssueNumber);
                return Ok(
                    new IssueCloseResult
                    {
                    });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("closeissue/{fromOwnerName}/{fromRepoName}")]
        public async Task<ActionResult<IssueCloseResult>> CloseIssue(
            string fromOwnerName, string fromRepoName,
            [FromBody] IssueCloseRequest issueCloseRequest)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            try
            {
                await gitHub.Issue.Update(fromOwnerName,
                         fromRepoName,
                         issueCloseRequest.IssueNumber,
                         new IssueUpdate
                         {
                             State = ItemState.Closed,
                         });
                return Ok(
                    new IssueCloseResult
                    {
                    });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
