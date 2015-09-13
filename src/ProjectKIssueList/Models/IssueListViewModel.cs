using System.Collections.Generic;
using Octokit;

namespace ProjectKIssueList.Models
{
    public class IssueWithRepo
    {
        public Issue Issue { get; set; }
        public string RepoName { get; set; }
    }

    public class PullRequestWithRepo
    {
        public PullRequest PullRequest { get; set; }
        public string RepoName { get; set; }
    }

    public class IssueListViewModel
    {
        public string GitHubUserName { get; set; }
        public string RepoSetName { get; set; }
        public string[] RepoSetNames { get; set; }
        public int TotalIssues { get; set; }
        public object WorkingIssues { get; set; }
        public object UntriagedIssues { get; set; }
        public string[] ReposIncluded { get; set; }
        public GroupByAssigneeViewModel GroupByAssignee { get; set; }
        public GroupByMilestoneViewModel GroupByMilestone { get; set; }
        public GroupByRepoViewModel GroupByRepo { get; set; }
        public List<PullRequestWithRepo> PullRequests { get; set; }
    }

    public class GroupByAssigneeViewModel
    {
        public IReadOnlyList<GroupByAssigneeAssignee> Assignees { get; set; }
    }

    public class GroupByAssigneeAssignee
    {
        public string Assignee { get; set; }
        public IReadOnlyList<IssueWithRepo> Issues { get; set; }
    }

    public class GroupByMilestoneViewModel
    {
        public IReadOnlyList<GroupByMilestoneMilestone> Milestones { get; set; }
    }

    public class GroupByMilestoneMilestone
    {
        public string Milestone { get; set; }
        public IReadOnlyList<IssueWithRepo> Issues { get; set; }
    }

    public class GroupByRepoViewModel
    {
        public IReadOnlyList<GroupByRepoRepo> Repos { get; set; }
    }

    public class GroupByRepoRepo
    {
        public string RepoName { get; set; }
        public IReadOnlyList<IssueWithRepo> Issues { get; set; }
    }
}
