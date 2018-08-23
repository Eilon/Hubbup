using System;
using System.Net.Http;
using System.Threading.Tasks;
using Hubbup.IssueMover.Dto;
using Microsoft.AspNetCore.Blazor;
using System.Collections.Generic;
using Microsoft.JSInterop;
using System.Linq;

namespace Hubbup.IssueMoverClient
{
    public class AppState
    {
        // Lets components receive change notifications
        // Could have whatever granularity you want (more events, hierarchy...)
        public event Action OnChange;

        private HttpClient Http { get; }

        public AppState(HttpClient http)
        {
            Http = http;
        }

        public string JsonData { get; set; }

        // Input fields
        public string FromValue { get; set; } = string.Empty;
        public string ToValue { get; set; } = string.Empty;

        // UI options
        public bool ShouldCreateDestinationLabels { get; set; } = true;
        public bool ShouldCreateDestinationMilestone { get; set; } = true;
        public bool ShouldLockOriginalIssue { get; set; } = true;

        // Operational data
        public IssueMoveData OriginalIssueMoveData { get; set; }
        public RepoMoveData DestinationRepoMoveData { get; set; }
        public bool IssueQueryInProgress { get; set; }
        public bool ToRepoQueryInProgress { get; set; }
        public bool MoveInProgress { get; set; }

        public string FromProgressBarText { get; set; }
        public ProgressBarStyle FromProgressBarStyle { get; set; }
        public string ToProgressBarText { get; set; }
        public ProgressBarStyle ToProgressBarStyle { get; set; }

        private static bool IsValidIssueFormat(string val)
        {
            var shortNameParts = val.Split('/', '#');
            if (shortNameParts.Length == 3)
            {
                if (int.TryParse(shortNameParts[2], out var issueNumber))
                {
                    return true;
                }
            }

            // Not valid format
            return false;
        }

        private static string NormalizeIssueFormat(string val)
        {
            // Normalize this pattern: https://github.com/aspnet/Universe/issues/123
            var GitHubPrefix = "https://github.com/";

            if (val.StartsWith(GitHubPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var fullUrlParts = val.Substring(GitHubPrefix.Length).Split('/');
                if (fullUrlParts.Length == 4 &&
                    fullUrlParts[2] == "issues")
                {
                    return fullUrlParts[0] + "/" + fullUrlParts[1] + "#" + fullUrlParts[3];
                }
            }

            // Unknown format, return original text
            return val;
        }

        private static bool IsValidRepoFormat(string val)
        {
            var shortNameParts = val.Split('/');
            if (shortNameParts.Length == 2)
            {
                return true;
            }

            // Not valid format
            return false;
        }

        private static string NormalizeRepoFormat(string val)
        {
            // Normalize this pattern: https://github.com/aspnet/Universe
            var GitHubPrefix = "https://github.com/";

            if (val.StartsWith(GitHubPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var fullUrlParts = val.Substring(GitHubPrefix.Length).Split('/');
                if (fullUrlParts.Length == 2)
                {
                    return fullUrlParts[0] + "/" + fullUrlParts[1];
                }
            }

            // Unknown format, return original text
            return val;
        }

        public bool ShouldMoveButtonBeEnabled()
        {
            return
                !MoveInProgress &&
                !IssueQueryInProgress &&
                !ToRepoQueryInProgress &&
                IsValidIssueFormat(FromValue) &&
                IsValidRepoFormat(ToValue);
        }

        public async Task OnFromInputBlur()
        {
            FromProgressBarText = "Looking for issue...";
            FromProgressBarStyle = ProgressBarStyle.InProgress;
            NotifyStateChanged();

            var val = FromValue;

            // If pattern is not in required format 'owner/repo#123', try to normalize
            if (!IsValidIssueFormat(val))
            {
                FromValue = NormalizeIssueFormat(val);
            }

            val = FromValue;

            // If pattern is STILL not in required format 'owner/repo#123', abort
            if (!IsValidIssueFormat(val))
            {
                // TODO: Set validation error somehow?
                FromProgressBarText = "Invalid issue format";
                FromProgressBarStyle = ProgressBarStyle.Error;
                return;
            }

            var shortNameParts = val.Split('/', '#');
            var fromOwner = shortNameParts[0];
            var fromRepo = shortNameParts[1];
            var fromIssueNumber = shortNameParts[2];

            IssueQueryInProgress = true;
            NotifyStateChanged();

            OriginalIssueMoveData = await Http.GetJsonAsync<IssueMoveData>($"/api/getmovedata/{fromOwner}/{fromRepo}/{fromIssueNumber}");
            AddJsonLog(OriginalIssueMoveData);
            IssueQueryInProgress = false;

            FromProgressBarText = $"Found issue #{OriginalIssueMoveData.Number}";
            FromProgressBarStyle = ProgressBarStyle.Success;
        }

        public async Task OnToInputBlur()
        {
            ToProgressBarText = "Looking for repo...";
            ToProgressBarStyle = ProgressBarStyle.InProgress;
            NotifyStateChanged();

            var val = ToValue;

            // If pattern is not in required format 'owner/repo', try to normalize
            if (!IsValidRepoFormat(val))
            {
                ToValue = NormalizeRepoFormat(val);
            }

            val = ToValue;

            // If pattern is STILL not in required format 'owner/repo', abort
            if (!IsValidRepoFormat(val))
            {
                // TODO: Set validation error somehow?
                ToProgressBarText = "Invalid repo format";
                ToProgressBarStyle = ProgressBarStyle.Error;
                return;
            }

            var shortNameParts = val.Split('/');
            var toOwner = shortNameParts[0];
            var toRepo = shortNameParts[1];

            ToRepoQueryInProgress = true;
            NotifyStateChanged();

            DestinationRepoMoveData = await Http.GetJsonAsync<RepoMoveData>($"/api/getrepodata/{toOwner}/{toRepo}");
            AddJsonLog(DestinationRepoMoveData);
            ToRepoQueryInProgress = false;

            ToProgressBarText = $"Found repo, {DestinationRepoMoveData.OpenIssueCount} open issue(s)";
            ToProgressBarStyle = ProgressBarStyle.Success;
        }

        private void AddJsonLog(object data)
        {
            var newJsonData = Json.Serialize(data);
            JsonData = $"@ {DateTimeOffset.Now}\r\n\r\n{newJsonData}\r\n\r\n{new string('-', 40)}\r\n\r\n{JsonData}";
        }

        public async Task OnMoveButtonClick(UIMouseEventArgs e)
        {
            try
            {
                MoveInProgress = true;

                IssueMoveStates = new List<IssueMoveState>();

                // Check From/To are valid
                // TODO: Is this needed? Would we be here if it wasn't valid?

                // Create destination labels
                if (ShouldCreateDestinationLabels)
                {
                    var createLabelState = new IssueMoveState { Description = "Creating labels" };
                    IssueMoveStates.Add(createLabelState);
                    NotifyStateChanged();

                    if (OriginalIssueMoveData.Labels.Any())
                    {
                        var labelCreateResult = await Http.PostJsonAsync<LabelCreateResult>($"/api/createlabels/{DestinationRepoMoveData.Owner}/{DestinationRepoMoveData.Repo}", new LabelCreateRequest { Labels = OriginalIssueMoveData.Labels, });
                        AddJsonLog(labelCreateResult);

                        createLabelState.Result = "Done!";
                        createLabelState.Success = true;
                        NotifyStateChanged();
                    }
                    else
                    {
                        createLabelState.Result = "Skipped (no labels on issue)";
                        createLabelState.Success = true;
                        NotifyStateChanged();
                    }
                }

                // Create destination milestone
                if (ShouldCreateDestinationMilestone)
                {
                    var createMilestoneState = new IssueMoveState { Description = "Creating milestone" };
                    IssueMoveStates.Add(createMilestoneState);
                    NotifyStateChanged();

                    if (!string.IsNullOrEmpty(OriginalIssueMoveData.Milestone))
                    {
                        var milestoneCreateResult = await Http.PostJsonAsync<MilestoneCreateResult>($"/api/createmilestone/{DestinationRepoMoveData.Owner}/{DestinationRepoMoveData.Repo}", new MilestoneCreateRequest { Milestone = OriginalIssueMoveData.Milestone, });
                        AddJsonLog(milestoneCreateResult);

                        createMilestoneState.Result = "Done!";
                        createMilestoneState.Success = true;
                        NotifyStateChanged();
                    }
                    else
                    {
                        createMilestoneState.Result = "Skipped (no milestone on issue)";
                        createMilestoneState.Success = true;
                        NotifyStateChanged();
                    }
                }

                // Create destination issue
                var moveIssueState = new IssueMoveState { Description = "Moving issue" };
                IssueMoveStates.Add(moveIssueState);
                NotifyStateChanged();

                var issueMoveResult = await Http.PostJsonAsync<IssueMoveResult>($"/api/moveissue/{DestinationRepoMoveData.Owner}/{DestinationRepoMoveData.Repo}",
                    new IssueMoveRequest
                    {
                        Title = OriginalIssueMoveData.Title,
                        Body = GetDestinationBody(OriginalIssueMoveData),
                        Assignees = OriginalIssueMoveData.Assignees,
                        Milestone = ShouldCreateDestinationMilestone ? OriginalIssueMoveData.Milestone : null,
                        Labels = ShouldCreateDestinationLabels ? OriginalIssueMoveData.Labels.Select(l => l.Text).ToArray() : null,
                    });
                AddJsonLog(issueMoveResult);

                moveIssueState.Result = "Done!";
                moveIssueState.Success = true;


                // Create destination comments
                var moveCommentState = new IssueMoveState { Description = "Moving comments" };
                IssueMoveStates.Add(moveCommentState);
                NotifyStateChanged();

                if (OriginalIssueMoveData.Comments.Any())
                {
                    for (var i = 0; i < OriginalIssueMoveData.Comments.Count; i++)
                    {
                        var commentData = OriginalIssueMoveData.Comments[i];

                        var commentMoveResult = await Http.PostJsonAsync<CommentMoveResult>($"/api/movecomment/{DestinationRepoMoveData.Owner}/{DestinationRepoMoveData.Repo}",
                            new CommentMoveRequest
                            {
                                IssueNumber = issueMoveResult.IssueNumber,
                                Text = GetDestinationComment(commentData.Author, commentData.Text, commentData.Date),
                            });
                        moveCommentState.Description = $"Moving comment {i + 1}/{OriginalIssueMoveData.Comments.Count}";
                        AddJsonLog(commentMoveResult);
                        NotifyStateChanged();
                    }

                    moveCommentState.Result = "Done!";
                    moveCommentState.Success = true;
                }
                else
                {
                    moveCommentState.Result = "Skipped (no comments)";
                    moveCommentState.Success = true;
                    NotifyStateChanged();
                }

                // Add old issue close message
                var addCloseCommentState = new IssueMoveState { Description = "Adding comment to original issue" };
                IssueMoveStates.Add(addCloseCommentState);
                NotifyStateChanged();

                var issueCloseCommentResult = await Http.PostJsonAsync<IssueCloseCommentResult>($"/api/closeissuecomment/{OriginalIssueMoveData.RepoOwner}/{OriginalIssueMoveData.RepoName}",
                    new IssueCloseCommentRequest
                    {
                        IssueNumber = OriginalIssueMoveData.Number,
                        Comment = $"This issue was moved to {DestinationRepoMoveData.Owner}/{DestinationRepoMoveData.Repo}#{issueMoveResult.IssueNumber}",
                    });
                AddJsonLog(issueCloseCommentResult);

                addCloseCommentState.Result = "Done!";
                addCloseCommentState.Success = true;

                // Lock old issue
                if (ShouldLockOriginalIssue)
                {
                    var lockIssueState = new IssueMoveState { Description = "Locking original issue" };
                    IssueMoveStates.Add(lockIssueState);
                    NotifyStateChanged();

                    var issueLockResult = await Http.PostJsonAsync<IssueLockResult>($"/api/lockissue/{OriginalIssueMoveData.RepoOwner}/{OriginalIssueMoveData.RepoName}",
                        new IssueLockRequest
                        {
                            IssueNumber = OriginalIssueMoveData.Number,
                        });
                    AddJsonLog(issueLockResult);

                    lockIssueState.Result = "Done!";
                    lockIssueState.Success = true;
                }

                // Close old issue
                var closeIssueState = new IssueMoveState { Description = "Closing original issue" };
                IssueMoveStates.Add(closeIssueState);
                NotifyStateChanged();

                var issueCloseResult = await Http.PostJsonAsync<IssueCloseResult>($"/api/closeissue/{OriginalIssueMoveData.RepoOwner}/{OriginalIssueMoveData.RepoName}",
                    new IssueCloseRequest
                    {
                        IssueNumber = OriginalIssueMoveData.Number,
                    });
                AddJsonLog(issueCloseResult);

                closeIssueState.Result = "Done!";
                closeIssueState.Success = true;


                // Reset states
                MoveInProgress = false;
            }
            catch (Exception ex)
            {
                AddJsonLog(new
                {
                    Message = "Exception occurred",
                    Exception = ex,
                });
            }
        }

        private static string GetDestinationBody(IssueMoveData issueToMove)
        {
            var dateTime = issueToMove.CreatedDate.ToLocalTime().DateTime;
            return $@"_From @{issueToMove.Author} on {dateTime.ToLongDateString()} {dateTime.ToLongTimeString()}_

{issueToMove.Body}

_Copied from original issue: {issueToMove.RepoOwner}/{issueToMove.RepoName}#{issueToMove.Number}_";
        }

        private static string GetDestinationComment(string author, string text, DateTimeOffset date)
        {
            var dateTime = date.ToLocalTime().DateTime;
            return $@"_From @{author} on {dateTime.ToLongDateString()} {dateTime.ToLongTimeString()}_

{text}";
        }

        public List<IssueMoveState> IssueMoveStates { get; set; }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }

    public class IssueMoveState
    {
        public string Description { get; set; }
        public string Result { get; set; }
        public bool Success { get; set; }
    }

}
