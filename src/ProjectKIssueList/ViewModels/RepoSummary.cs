using ProjectKIssueList.Models;

namespace ProjectKIssueList.ViewModels
{
    public class RepoSummary
    {
        public RepoDefinition Repo { get; set; }

        public int OpenIssues { get; set; }
        public int UnassignedIssues { get; set; }
        public int WorkingIssues { get; set; }

        public int OpenPRs { get; set; }
        public int StalePRs { get; set; }
    }
}
