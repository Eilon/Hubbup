using System.Threading.Tasks;
using Hubbup.IssueMover.Dto;

namespace Hubbup.IssueMoverApi
{
    public interface IIssueMoverService
    {
        Task<IssueCloseResult> CloseIssue(string originalOwner, string originalRepo, IssueCloseRequest issueCloseRequest);
        Task<IssueCloseCommentResult> CloseIssueComment(string originalOwner, string originalRepo, IssueCloseCommentRequest issueCloseCommentRequest);
        Task<LabelCreateResult> CreateLabels(string destinationOwner, string destinationRepo, LabelCreateRequest labelCreateRequest);
        Task<MilestoneCreateResult> CreateMilestone(string destinationOwner, string destinationRepo, MilestoneCreateRequest milestoneCreateRequest);
        Task<IssueMoveData> GetIssueMoveData(string fromOwner, string fromRepo, int fromIssueNumber);
        Task<RepoMoveData> GetRepoData(string toOwner, string toRepo);
        Task<IssueLockResult> LockIssue(string originalOwner, string originalRepo, IssueLockRequest issueLockRequest);
        Task<CommentMoveResult> MoveComment(string destinationOwner, string destinationRepo, CommentMoveRequest commentMoveRequest);
        Task<IssueMoveResult> MoveIssue(string destinationOwner, string destinationRepo, IssueMoveRequest issueMoveRequest);
    }
}