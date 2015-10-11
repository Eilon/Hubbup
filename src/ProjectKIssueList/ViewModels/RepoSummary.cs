using ProjectKIssueList.Models;

namespace ProjectKIssueList.ViewModels
{
    public class RepoSummary
    {
        public RepoDefinition Repo { get; set; }

        public int OpenIssues { get; set; }
        public string OpenIssuesQueryUrl { get; set; }
        public int UntriagedIssues { get; set; }
        public string UntriagedIssuesQueryUrl { get; set; }
        public int UnassignedIssues { get; set; }
        public string UnassignedIssuesQueryUrl { get; set; }
        public int WorkingIssues { get; set; }
        public string WorkingIssuesQueryUrl { get; set; }

        public int OpenPRs { get; set; }
        public string OpenPRsQueryUrl { get; set; }
        public int StalePRs { get; set; }
        public string StalePRsQueryUrl { get; set; }
    }
}
