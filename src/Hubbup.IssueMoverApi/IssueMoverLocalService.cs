using System;
using System.Linq;
using System.Threading.Tasks;
using Hubbup.IssueMover.Dto;
using Octokit;

namespace Hubbup.IssueMoverApi
{
    public class IssueMoverLocalService : IIssueMoverService
    {
        public IssueMoverLocalService(IGitHubAccessor gitHubAccessor)
        {
            GitHubAccessor = gitHubAccessor;
        }

        public IGitHubAccessor GitHubAccessor { get; }

        public async Task<IssueCloseResult> CloseIssue(string originalOwner, string originalRepo, IssueCloseRequest issueCloseRequest)
        {
            var gitHub = await GitHubAccessor.GetGitHubClient();

            await gitHub.Issue.Update(originalOwner,
                 originalRepo,
                 issueCloseRequest.IssueNumber,
                 new IssueUpdate
                 {
                     State = ItemState.Closed,
                 });
            return new IssueCloseResult
            {
            };
        }

        public async Task<IssueCloseCommentResult> CloseIssueComment(string originalOwner, string originalRepo, IssueCloseCommentRequest issueCloseCommentRequest)
        {
            var gitHub = await GitHubAccessor.GetGitHubClient();

            await gitHub.Issue.Comment.Create(originalOwner, originalRepo, issueCloseCommentRequest.IssueNumber, issueCloseCommentRequest.Comment);

            return new IssueCloseCommentResult
            {
            };
        }

        public async Task<LabelCreateResult> CreateLabels(string destinationOwner, string destinationRepo, LabelCreateRequest labelCreateRequest)
        {
            var gitHub = await GitHubAccessor.GetGitHubClient();

            var destinationLabels = await gitHub.Issue.Labels.GetAllForRepository(destinationOwner, destinationRepo);

            var listOfLabelsToCreate = labelCreateRequest.Labels
                .Where(labelNeeded =>
                    !destinationLabels
                        .Any(destinationLabel =>
                            string.Equals(
                                labelNeeded.Text,
                                destinationLabel.Name,
                                StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var labelToCreate in listOfLabelsToCreate)
            {
                await gitHub.Issue.Labels.Create(destinationOwner, destinationRepo, new NewLabel(labelToCreate.Text, labelToCreate.Color));
            }

            return new LabelCreateResult
            {
                LabelsCreated = listOfLabelsToCreate,
            };
        }

        public async Task<MilestoneCreateResult> CreateMilestone(string destinationOwner, string destinationRepo, MilestoneCreateRequest milestoneCreateRequest)
        {
            var gitHub = await GitHubAccessor.GetGitHubClient();

            var destinationMilestones = await gitHub.Issue.Milestone.GetAllForRepository(destinationOwner, destinationRepo);
            if (destinationMilestones.Any(m => string.Equals(m.Title, milestoneCreateRequest.Milestone, StringComparison.OrdinalIgnoreCase)))
            {
                // Milestone already exists, so do nothing
                return new MilestoneCreateResult
                {
                    MilestoneCreated = null,
                };
            }

            await gitHub.Issue.Milestone.Create(destinationOwner, destinationRepo, new NewMilestone(milestoneCreateRequest.Milestone));

            return new MilestoneCreateResult
            {
                MilestoneCreated = milestoneCreateRequest.Milestone,
            };
        }

        private static IssueState GetIssueState(ItemState itemState)
        {
            if (itemState == ItemState.Open)
            {
                return IssueState.Open;
            }
            return IssueState.Closed;
        }

        public async Task<IssueMoveData> GetIssueMoveData(string fromOwner, string fromRepo, int fromIssueNumber)
        {
            var gitHub = await GitHubAccessor.GetGitHubClient();

            var fromIssue = await gitHub.Issue.Get(fromOwner, fromRepo, fromIssueNumber);

            var comments =
                (await gitHub.Issue.Comment.GetAllForIssue(fromOwner, fromRepo, fromIssueNumber))
                    .Select(issueComment =>
                        new CommentData
                        {
                            Author = issueComment.User.Login,
                            Text = issueComment.Body,
                            Date = issueComment.CreatedAt,
                        })
                    .ToList();

            return new IssueMoveData
            {
                RepoOwner = fromOwner,
                RepoName = fromRepo,
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
            };
        }

        public async Task<RepoMoveData> GetRepoData(string toOwner, string toRepo)
        {
            var gitHub = await GitHubAccessor.GetGitHubClient();

            var repo = await gitHub.Repository.Get(toOwner, toRepo);

            return new RepoMoveData
            {
                Owner = repo.Owner?.Login,
                Repo = repo.Name,
                OpenIssueCount = repo.OpenIssuesCount,
            };
        }

        public async Task<IssueLockResult> LockIssue(string originalOwner, string originalRepo, IssueLockRequest issueLockRequest)
        {
            var gitHub = await GitHubAccessor.GetGitHubClient();

            await gitHub.Issue.Lock(originalOwner,
                     originalRepo,
                     issueLockRequest.IssueNumber);
            return new IssueLockResult
            {
            };
        }

        public async Task<CommentMoveResult> MoveComment(string destinationOwner, string destinationRepo, CommentMoveRequest commentMoveRequest)
        {
            var gitHub = await GitHubAccessor.GetGitHubClient();

            await gitHub.Issue.Comment.Create(destinationOwner, destinationRepo, commentMoveRequest.IssueNumber, commentMoveRequest.Text);

            return new CommentMoveResult
            {
            };
        }

        public async Task<IssueMoveResult> MoveIssue(string destinationOwner, string destinationRepo, IssueMoveRequest issueMoveRequest)
        {
            var gitHub = await GitHubAccessor.GetGitHubClient();

            var destinationMilestones = await gitHub.Issue.Milestone.GetAllForRepository(destinationOwner, destinationRepo);

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

            var newIssueCreated = await gitHub.Issue.Create(destinationOwner, destinationRepo, newIssueDetails);

            return new IssueMoveResult
            {
                IssueNumber = newIssueCreated.Number,
                HtmlUrl = newIssueCreated.HtmlUrl,
            };
        }
    }
}
