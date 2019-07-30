using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Hubbup.IssueMover.Dto;
using Hubbup.IssueMoverApi;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Hubbup.IssueMoverClient
{
    public class AppState
    {
        // Lets components receive change notifications
        // Could have whatever granularity you want (more events, hierarchy...)
        public event Action OnChange;

        public IIssueMoverService IssueMoverService { get; }


        public AppState(IIssueMoverService issueMoverService)
        {
            IssueMoverService = issueMoverService;
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
                if (int.TryParse(shortNameParts[2], out _))
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
                IsValidRepoFormat(ToValue) &&
                IsValidIssue() &&
                OriginalIssueMoveData != null &&
                DestinationRepoMoveData != null;
        }

        private bool IsValidIssue()
        {
            return OriginalIssueMoveData != null &&
                OriginalIssueMoveData.State == IssueState.Open &&
                !OriginalIssueMoveData.IsPullRequest;
        }

        public async Task OnFromInputBlur()
        {
            if (string.IsNullOrEmpty(FromValue))
            {
                FromProgressBarText = "";
                FromProgressBarStyle = ProgressBarStyle.None;
                OriginalIssueMoveData = null;
                return;
            }

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
            var fromIssueNumber = int.Parse(shortNameParts[2], CultureInfo.InvariantCulture);

            IssueQueryInProgress = true;
            NotifyStateChanged();

            IErrorResult getMoveDataError = null;
            try
            {
                OriginalIssueMoveData = await IssueMoverService.GetIssueMoveData(fromOwner, fromRepo, fromIssueNumber);
                if (OriginalIssueMoveData.HasErrors())
                {
                    getMoveDataError = OriginalIssueMoveData;
                }
            }
            catch (Exception ex)
            {
                getMoveDataError = new ErrorResult
                {
                    ExceptionMessage = ex.Message,
                    ExceptionStackTrace = ex.StackTrace,
                };
            }

            if (getMoveDataError != null)
            {
                OriginalIssueMoveData = null;
                AddJsonLog(new ErrorLogEntry
                {
                    Description = "Error calling 'getmovedata'",
                    ErrorResult = getMoveDataError,
                });
                IssueQueryInProgress = false;

                FromProgressBarText = $"Error!";
                FromProgressBarStyle = ProgressBarStyle.Error;
                return;
            }

            AddJsonLog(OriginalIssueMoveData);
            IssueQueryInProgress = false;

            if (OriginalIssueMoveData.IsPullRequest)
            {
                FromProgressBarText = $"Found #{OriginalIssueMoveData.Number}, but it's a pull request";
                FromProgressBarStyle = ProgressBarStyle.Error;
            }
            else if (OriginalIssueMoveData.State == IssueState.Closed)
            {
                FromProgressBarText = $"Found #{OriginalIssueMoveData.Number}, but it's closed";
                FromProgressBarStyle = ProgressBarStyle.Error;
            }
            else
            {
                FromProgressBarText = $"Found issue #{OriginalIssueMoveData.Number}";
                FromProgressBarStyle = ProgressBarStyle.Success;
            }
        }

        public async Task OnToInputBlur()
        {
            if (string.IsNullOrEmpty(ToValue))
            {
                ToProgressBarText = "";
                ToProgressBarStyle = ProgressBarStyle.None;
                DestinationRepoMoveData = null;
                return;
            }

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

            IErrorResult getRepoDataError = null;
            try
            {
                DestinationRepoMoveData = await IssueMoverService.GetRepoData(toOwner, toRepo);
                if (DestinationRepoMoveData.HasErrors())
                {
                    getRepoDataError = DestinationRepoMoveData;
                }
            }
            catch (Exception ex)
            {
                getRepoDataError = new ErrorResult
                {
                    ExceptionMessage = ex.Message,
                    ExceptionStackTrace = ex.StackTrace,
                };
            }

            if (getRepoDataError != null)
            {
                DestinationRepoMoveData = null;
                AddJsonLog(new ErrorLogEntry
                {
                    Description = "Error calling 'getrepodata'",
                    ErrorResult = getRepoDataError,
                });
                ToRepoQueryInProgress = false;

                ToProgressBarText = $"Error!";
                ToProgressBarStyle = ProgressBarStyle.Error;
                return;
            }

            AddJsonLog(DestinationRepoMoveData);
            ToRepoQueryInProgress = false;

            ToProgressBarText = $"Found repo, {DestinationRepoMoveData.OpenIssueCount} open issue(s)";
            ToProgressBarStyle = ProgressBarStyle.Success;
        }

        private void AddJsonLog(object data)
        {
            string newJsonData;
            try
            {
                newJsonData = JsonSerializer.Serialize(data);
            }
            catch (Exception e)
            {
                newJsonData = $"Couldn't serialize data type '{data?.GetType().FullName ?? "<null>"}': {e.Message}\r\n{e.StackTrace}";
            }
            JsonData = $"@ {DateTimeOffset.Now}\r\n\r\n{newJsonData}\r\n\r\n{new string('-', 40)}\r\n\r\n{JsonData}";
        }

        public async Task OnMoveButtonClick(UIMouseEventArgs e)
        {
            IssueMoveStates = new List<IssueMoveState>();

            try
            {
                MoveInProgress = true;

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
                        IErrorResult labelCreateResultError = null;
                        try
                        {
                            var labelCreateResult = await IssueMoverService.CreateLabels(DestinationRepoMoveData.Owner, DestinationRepoMoveData.Repo, new LabelCreateRequest { Labels = OriginalIssueMoveData.Labels, });
                            if (labelCreateResult.HasErrors())
                            {
                                labelCreateResultError = labelCreateResult;
                            }
                            AddJsonLog(labelCreateResult);
                        }
                        catch (Exception ex)
                        {
                            labelCreateResultError = new ErrorResult
                            {
                                ExceptionMessage = ex.Message,
                                ExceptionStackTrace = ex.StackTrace,
                            };
                        }

                        if (labelCreateResultError != null)
                        {
                            AddJsonLog(new ErrorLogEntry
                            {
                                Description = "Error calling 'createlabels'",
                                ErrorResult = labelCreateResultError,
                            });

                            createLabelState.Result = "Error! (skipping)";
                            createLabelState.Success = false;
                            NotifyStateChanged();

                            // No need to abort if this failed because it's optional, so continue to next step
                        }
                        else
                        {
                            createLabelState.Result = "Done!";
                            createLabelState.Success = true;
                            NotifyStateChanged();
                        }
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
                        IErrorResult milestoneCreateResultError = null;
                        try
                        {
                            var milestoneCreateResult = await IssueMoverService.CreateMilestone(DestinationRepoMoveData.Owner, DestinationRepoMoveData.Repo, new MilestoneCreateRequest { Milestone = OriginalIssueMoveData.Milestone, });
                            if (milestoneCreateResult.HasErrors())
                            {
                                milestoneCreateResultError = milestoneCreateResult;
                            }
                            AddJsonLog(milestoneCreateResult);
                        }
                        catch (Exception ex)
                        {
                            milestoneCreateResultError = new ErrorResult
                            {
                                ExceptionMessage = ex.Message,
                                ExceptionStackTrace = ex.StackTrace,
                            };
                        }

                        if (milestoneCreateResultError != null)
                        {
                            AddJsonLog(new ErrorLogEntry
                            {
                                Description = "Error calling 'createmilestone'",
                                ErrorResult = milestoneCreateResultError,
                            });

                            createMilestoneState.Result = "Error! (skipping)";
                            createMilestoneState.Success = false;
                            NotifyStateChanged();

                            // No need to abort if this failed because it's optional, so continue to next step
                        }
                        else
                        {
                            createMilestoneState.Result = "Done!";
                            createMilestoneState.Success = true;
                            NotifyStateChanged();
                        }
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

                var destinationIssueNumber = -1;
                var destinationIssueHtmlUrl = string.Empty;

                IErrorResult issueMoveResultError = null;
                try
                {
                    var issueMoveResult = await IssueMoverService.MoveIssue(DestinationRepoMoveData.Owner, DestinationRepoMoveData.Repo,
                        new IssueMoveRequest
                        {
                            Title = OriginalIssueMoveData.Title,
                            Body = GetDestinationBody(OriginalIssueMoveData),
                            Assignees = OriginalIssueMoveData.Assignees,
                            Milestone = ShouldCreateDestinationMilestone ? OriginalIssueMoveData.Milestone : null,
                            Labels = ShouldCreateDestinationLabels ? OriginalIssueMoveData.Labels.Select(l => l.Text).ToArray() : null,
                        });
                    if (issueMoveResult.HasErrors())
                    {
                        issueMoveResultError = issueMoveResult;
                    }
                    AddJsonLog(issueMoveResult);

                    destinationIssueNumber = issueMoveResult.IssueNumber;
                    destinationIssueHtmlUrl = issueMoveResult.HtmlUrl;
                }
                catch (Exception ex)
                {
                    issueMoveResultError = new ErrorResult
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    };
                }

                if (issueMoveResultError != null)
                {
                    AddJsonLog(new ErrorLogEntry
                    {
                        Description = "Error calling 'moveissue'",
                        ErrorResult = issueMoveResultError,
                    });

                    moveIssueState.Result = "Error!";
                    moveIssueState.Success = false;

                    IssueMoveStates.Add(new IssueMoveState
                    {
                        StateType = IssueMoveStateType.FatalError,
                        ErrorResult = issueMoveResultError,
                        Description = "Fatal error",
                    });
                    return;
                }

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

                        IErrorResult commentMoveResultError = null;
                        try
                        {
                            var commentMoveResult = await IssueMoverService.MoveComment(DestinationRepoMoveData.Owner, DestinationRepoMoveData.Repo,
                                new CommentMoveRequest
                                {
                                    IssueNumber = destinationIssueNumber,
                                    Text = GetDestinationComment(commentData.Author, commentData.Text, commentData.Date),
                                });
                            if (commentMoveResult.HasErrors())
                            {
                                commentMoveResultError = commentMoveResult;
                            }
                            moveCommentState.Description = $"Moving comment {i + 1}/{OriginalIssueMoveData.Comments.Count}";
                            AddJsonLog(commentMoveResult);
                        }
                        catch (Exception ex)
                        {
                            commentMoveResultError = new ErrorResult
                            {
                                ExceptionMessage = ex.Message,
                                ExceptionStackTrace = ex.StackTrace,
                            };
                        }

                        if (commentMoveResultError != null)
                        {
                            AddJsonLog(new ErrorLogEntry
                            {
                                Description = $"Error calling 'movecomment' for comment #{i + 1}",
                                ErrorResult = commentMoveResultError,
                            });

                            moveCommentState.Result = "Error!";
                            moveCommentState.Success = false;

                            IssueMoveStates.Add(new IssueMoveState
                            {
                                StateType = IssueMoveStateType.FatalError,
                                ErrorResult = commentMoveResultError,
                                Description = "Fatal error",
                            });
                            return;
                        }

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

                IErrorResult closeCommentResultError = null;
                try
                {
                    var issueCloseCommentResult = await IssueMoverService.CloseIssueComment(OriginalIssueMoveData.RepoOwner, OriginalIssueMoveData.RepoName,
                        new IssueCloseCommentRequest
                        {
                            IssueNumber = OriginalIssueMoveData.Number,
                            Comment = $"This issue was moved to {DestinationRepoMoveData.Owner}/{DestinationRepoMoveData.Repo}#{destinationIssueNumber}",
                        });
                    if (issueCloseCommentResult.HasErrors())
                    {
                        closeCommentResultError = issueCloseCommentResult;
                    }
                    AddJsonLog(issueCloseCommentResult);
                }
                catch (Exception ex)
                {
                    closeCommentResultError = new ErrorResult
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    };
                }

                if (closeCommentResultError != null)
                {
                    AddJsonLog(new ErrorLogEntry
                    {
                        Description = $"Error calling 'closeissuecomment'",
                        ErrorResult = closeCommentResultError,
                    });

                    addCloseCommentState.Result = "Error!";
                    addCloseCommentState.Success = false;

                    IssueMoveStates.Add(new IssueMoveState
                    {
                        StateType = IssueMoveStateType.FatalError,
                        ErrorResult = closeCommentResultError,
                        Description = "Fatal error",
                    });
                    return;
                }

                addCloseCommentState.Result = "Done!";
                addCloseCommentState.Success = true;

                // Lock old issue
                if (ShouldLockOriginalIssue)
                {
                    var lockIssueState = new IssueMoveState { Description = "Locking original issue" };
                    IssueMoveStates.Add(lockIssueState);
                    NotifyStateChanged();

                    IErrorResult lockIssueResultError = null;
                    try
                    {
                        var issueLockResult = await IssueMoverService.LockIssue(OriginalIssueMoveData.RepoOwner, OriginalIssueMoveData.RepoName,
                            new IssueLockRequest
                            {
                                IssueNumber = OriginalIssueMoveData.Number,
                            });
                        if (issueLockResult.HasErrors())
                        {
                            lockIssueResultError = issueLockResult;
                        }
                        AddJsonLog(issueLockResult);
                    }
                    catch (Exception ex)
                    {
                        lockIssueResultError = new ErrorResult
                        {
                            ExceptionMessage = ex.Message,
                            ExceptionStackTrace = ex.StackTrace,
                        };
                    }

                    if (lockIssueResultError != null)
                    {
                        AddJsonLog(new ErrorLogEntry
                        {
                            Description = $"Error calling 'lockissue'",
                            ErrorResult = lockIssueResultError,
                        });

                        lockIssueState.Result = "Error! (skipping)";
                        lockIssueState.Success = false;
                        NotifyStateChanged();

                        // No need to abort if this failed because it's optional, so continue to next step
                    }
                    else
                    {
                        lockIssueState.Result = "Done!";
                        lockIssueState.Success = true;
                        NotifyStateChanged();
                    }
                }

                // Close old issue
                var closeIssueState = new IssueMoveState { Description = "Closing original issue" };
                IssueMoveStates.Add(closeIssueState);
                NotifyStateChanged();

                IErrorResult closeIssueResultError = null;
                try
                {
                    var issueCloseResult = await IssueMoverService.CloseIssue(OriginalIssueMoveData.RepoOwner, OriginalIssueMoveData.RepoName,
                        new IssueCloseRequest
                        {
                            IssueNumber = OriginalIssueMoveData.Number,
                        });
                    if (issueCloseResult.HasErrors())
                    {
                        closeIssueResultError = issueCloseResult;
                    }
                    AddJsonLog(issueCloseResult);
                }
                catch (Exception ex)
                {
                    closeIssueResultError = new ErrorResult
                    {
                        ExceptionMessage = ex.Message,
                        ExceptionStackTrace = ex.StackTrace,
                    };
                }

                if (closeIssueResultError != null)
                {
                    AddJsonLog(new ErrorLogEntry
                    {
                        Description = $"Error calling 'closeissue'",
                        ErrorResult = closeIssueResultError,
                    });

                    closeIssueState.Result = "Error!";
                    closeIssueState.Success = false;

                    IssueMoveStates.Add(new IssueMoveState
                    {
                        StateType = IssueMoveStateType.FatalError,
                        ErrorResult = closeIssueResultError,
                        Description = "Fatal error",
                    });
                    return;
                }

                closeIssueState.Result = "Done!";
                closeIssueState.Success = true;
                NotifyStateChanged();


                IssueMoveStates.Add(new IssueMoveState
                {
                    StateType = IssueMoveStateType.LinkResult,
                    Description = $"Moved to new issue #{destinationIssueNumber}",
                    Link = destinationIssueHtmlUrl,
                });


                // Reset states
                MoveInProgress = false;
            }
            catch (Exception ex)
            {
                var overallErrorResult = new ErrorResult
                {
                    ExceptionMessage = ex.Message,
                    ExceptionStackTrace = ex.StackTrace,
                };
                AddJsonLog(new ErrorLogEntry
                {
                    Description = "Unknown error during move operation",
                    ErrorResult = overallErrorResult,
                });

                var outerState = new IssueMoveState { Description = "Error" };
                outerState.Result = "Unknown error during move operation";
                outerState.Success = false;
                IssueMoveStates.Add(outerState);

                IssueMoveStates.Add(new IssueMoveState
                {
                    StateType = IssueMoveStateType.FatalError,
                    ErrorResult = overallErrorResult,
                    Description = "Fatal error",
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
}
