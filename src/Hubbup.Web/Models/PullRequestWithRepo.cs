using Octokit;

namespace Hubbup.Web.Models
{
    public class PullRequestWithRepo
    {
        public PullRequest PullRequest { get; set; }
        public RepoDefinition Repo { get; set; }
        public bool IsInAssociatedPersonSet { get; set; }
    }
}
