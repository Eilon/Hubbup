using Octokit;

namespace ProjectKIssueList.Models
{
    public class PullRequestWithRepo
    {
        public PullRequest PullRequest { get; set; }
        public RepoDefinition Repo { get; set; }
        public bool IsInAssociatedPersonSet { get; set; }
    }
}
