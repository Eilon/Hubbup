using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Hubbup.IssueMover.Dto;
using Microsoft.AspNetCore.Components;

namespace Hubbup.IssueMoverApi
{
    public class IssueMoverHttpService : IIssueMoverService
    {
        public IssueMoverHttpService()
        {
            Http = new HttpClient();
        }

        public HttpClient Http { get; }

        public async Task<IssueMoveData> GetIssueMoveData(string fromOwner, string fromRepo, int fromIssueNumber)
        {
            return await Http.GetJsonAsync<IssueMoveData>($"https://localhost:44347/api/getmovedata/{fromOwner}/{fromRepo}/{fromIssueNumber.ToString(CultureInfo.InvariantCulture)}");
        }

        public async Task<RepoMoveData> GetRepoData(string toOwner, string toRepo)
        {
            return await Http.GetJsonAsync<RepoMoveData>($"https://localhost:44347/api/getrepodata/{toOwner}/{toRepo}");
        }

        public async Task<LabelCreateResult> CreateLabels(string destinationOwner, string destinationRepo, LabelCreateRequest labelCreateRequest)
        {
            return await Http.PostJsonAsync<LabelCreateResult>($"https://localhost:44347/api/createlabels/{destinationOwner}/{destinationRepo}", labelCreateRequest);
        }

        public async Task<MilestoneCreateResult> CreateMilestone(string destinationOwner, string destinationRepo, MilestoneCreateRequest milestoneCreateRequest)
        {
            return await Http.PostJsonAsync<MilestoneCreateResult>($"https://localhost:44347/api/createmilestone/{destinationOwner}/{destinationRepo}", milestoneCreateRequest);
        }

        public async Task<IssueMoveResult> MoveIssue(string destinationOwner, string destinationRepo, IssueMoveRequest issueMoveRequest)
        {
            return await Http.PostJsonAsync<IssueMoveResult>($"https://localhost:44347/api/moveissue/{destinationOwner}/{destinationRepo}", issueMoveRequest);
        }

        public async Task<CommentMoveResult> MoveComment(string destinationOwner, string destinationRepo, CommentMoveRequest commentMoveRequest)
        {
            return await Http.PostJsonAsync<CommentMoveResult>($"https://localhost:44347/api/movecomment/{destinationOwner}/{destinationRepo}", commentMoveRequest);
        }

        public async Task<IssueCloseCommentResult> CloseIssueComment(string originalOwner, string originalRepo, IssueCloseCommentRequest issueCloseCommentRequest)
        {
            return await Http.PostJsonAsync<IssueCloseCommentResult>($"https://localhost:44347/api/closeissuecomment/{originalOwner}/{originalRepo}", issueCloseCommentRequest);
        }

        public async Task<IssueLockResult> LockIssue(string originalOwner, string originalRepo, IssueLockRequest issueLockRequest)
        {
            return await Http.PostJsonAsync<IssueLockResult>($"https://localhost:44347/api/lockissue/{originalOwner}/{originalRepo}", issueLockRequest);
        }

        public async Task<IssueCloseResult> CloseIssue(string originalOwner, string originalRepo, IssueCloseRequest issueCloseRequest)
        {
            return await Http.PostJsonAsync<IssueCloseResult>($"https://localhost:44347/api/closeissue/{originalOwner}/{originalRepo}", issueCloseRequest);
        }
    }
}
