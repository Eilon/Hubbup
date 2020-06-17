using Hubbup.IssueMover.Dto;
using Hubbup.IssueMoverApi;
using Hubbup.Web.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Hubbup.Web.Controllers
{
    [Route("api")]
    [Authorize]
    public class MoverApiController : Controller
    {
        private readonly IDataSource _dataSource;
        private readonly ILogger<IssuesApiController> _logger;

        public IIssueMoverService IssueMoverService { get; }

        public MoverApiController(IDataSource dataSource, ILogger<IssuesApiController> logger, IIssueMoverService issueMoverService)
        {
            _dataSource = dataSource;
            _logger = logger;
            IssueMoverService = issueMoverService;
        }

        [Route("getmovedata/{fromOwnerName}/{fromRepoName}/{fromIssueNumber}")]
        public async Task<IActionResult> GetMoveData(string fromOwnerName, string fromRepoName, string fromIssueNumber)
        {
            if (!int.TryParse(fromIssueNumber, out var fromIssueNumberInt))
            {
                return BadRequest(
                    new IssueMoveData
                    {
                        ErrorMessage = $"Issue number is invalid: {fromIssueNumber}",
                    });
            }

            try
            {
                var issueMoveData = await IssueMoverService.GetIssueMoveData(fromOwnerName, fromRepoName, fromIssueNumberInt);

                return Ok(issueMoveData);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new IssueMoveData
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    });
            }
        }

        [Route("getrepodata/{toOwnerName}/{toRepoName}")]
        public async Task<IActionResult> GetRepoData(string toOwnerName, string toRepoName)
        {
            try
            {
                var repo = await IssueMoverService.GetRepoData(toOwnerName, toRepoName);

                return Ok(repo);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new RepoMoveData
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    });
            }
        }

        [HttpPost("createlabels/{toOwnerName}/{toRepoName}")]
        public async Task<ActionResult<LabelCreateResult>> CreateLabels(
            string toOwnerName, string toRepoName,
            [FromBody] LabelCreateRequest labelCreateRequest)
        {
            try
            {
                var labelCreateResult = await IssueMoverService.CreateLabels(toOwnerName, toRepoName, labelCreateRequest);

                return Ok(labelCreateResult);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new LabelCreateResult
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    });
            }
        }

        [HttpPost("createmilestone/{toOwnerName}/{toRepoName}")]
        public async Task<ActionResult<LabelCreateResult>> CreateMilestone(
            string toOwnerName, string toRepoName,
            [FromBody] MilestoneCreateRequest milestoneCreateRequest)
        {
            try
            {
                var milestoneCreateResult = await IssueMoverService.CreateMilestone(toOwnerName, toRepoName, milestoneCreateRequest);

                return Ok(milestoneCreateResult);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new MilestoneCreateResult
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    });
            }
        }

        [HttpPost("moveissue/{toOwnerName}/{toRepoName}")]
        public async Task<ActionResult<IssueMoveResult>> MoveIssue(
            string toOwnerName, string toRepoName,
            [FromBody] IssueMoveRequest issueMoveRequest)
        {
            try
            {
                var issueMoveresult = await IssueMoverService.MoveIssue(toOwnerName, toRepoName, issueMoveRequest);

                return Ok(issueMoveresult);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new IssueMoveResult
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    });
            }
        }

        [HttpPost("movecomment/{toOwnerName}/{toRepoName}")]
        public async Task<ActionResult<CommentMoveResult>> MoveComment(
            string toOwnerName, string toRepoName,
            [FromBody] CommentMoveRequest commentMoveRequest)
        {
            try
            {
                var commentMoveResult = await IssueMoverService.MoveComment(toOwnerName, toRepoName, commentMoveRequest);

                return Ok(commentMoveResult);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new CommentMoveResult
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    });
            }
        }

        [HttpPost("closeissuecomment/{fromOwnerName}/{fromRepoName}")]
        public async Task<ActionResult<IssueCloseCommentResult>> CloseIssueComment(
            string fromOwnerName, string fromRepoName,
            [FromBody] IssueCloseCommentRequest closeCommentRequest)
        {
            try
            {
                var issueCloseCommentResult = await IssueMoverService.CloseIssueComment(fromOwnerName, fromRepoName, closeCommentRequest);

                return Ok(issueCloseCommentResult);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new IssueCloseCommentResult
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    });
            }
        }

        [HttpPost("lockissue/{fromOwnerName}/{fromRepoName}")]
        public async Task<ActionResult<IssueLockResult>> LockIssue(
            string fromOwnerName, string fromRepoName,
            [FromBody] IssueLockRequest issueLockRequest)
        {
            try
            {
                var issueLockResult = await IssueMoverService.LockIssue(fromOwnerName, fromRepoName, issueLockRequest);

                return Ok(issueLockResult);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new IssueLockResult
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    });
            }
        }

        [HttpPost("closeissue/{fromOwnerName}/{fromRepoName}")]
        public async Task<ActionResult<IssueCloseResult>> CloseIssue(
            string fromOwnerName, string fromRepoName,
            [FromBody] IssueCloseRequest issueCloseRequest)
        {
            try
            {
                var issueCloseResult = await IssueMoverService.CloseIssue(fromOwnerName, fromRepoName, issueCloseRequest);

                return Ok(issueCloseResult);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new IssueCloseResult
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    });
            }
        }
    }
}
