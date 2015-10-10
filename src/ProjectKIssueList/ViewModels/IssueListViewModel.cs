using System.Collections.Generic;
using ProjectKIssueList.Models;

namespace ProjectKIssueList.ViewModels
{
    public class IssueListViewModel
    {
        public List<RepoFailure> RepoFailures { get; set; }

        public string GitHubUserName { get; set; }
        public string LastUpdated { get; set; }

        public string RepoSetName { get; set; }
        public string[] RepoSetNames { get; set; }
        public int TotalIssues { get; set; }
        public object WorkingIssues { get; set; }
        public object UntriagedIssues { get; set; }
        public List<RepoSummary> ReposIncluded { get; set; }

        public string OpenIssuesQuery { get; set; }
        public string WorkingIssuesQuery { get; set; }
        public string UntriagedIssuesQuery { get; set; }
        public string OpenPRsQuery { get; set; }
        public string StalePRsQuery { get; set; }

        public GroupByAssigneeViewModel GroupByAssignee { get; set; }
        public GroupByMilestoneViewModel GroupByMilestone { get; set; }
        public GroupByRepoViewModel GroupByRepo { get; set; }
        public List<PullRequestWithRepo> PullRequests { get; set; }
    }
}
