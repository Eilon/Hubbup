using System;
using System.Collections.Generic;
using Octokit;

namespace ProjectKIssueList.Models
{
    public class IssueWithRepo
    {
        public Issue Issue { get; set; }
        public RepoDefinition Repo { get; set; }
        public DateTimeOffset? WorkingStartTime { get; set; }
        public bool IsInAssociatedPersonSet { get; set; }
    }

    public class PullRequestWithRepo
    {
        public PullRequest PullRequest { get; set; }
        public RepoDefinition Repo { get; set; }
        public bool IsInAssociatedPersonSet { get; set; }
    }

    public class IssueListViewModel
    {
        public string GitHubUserName { get; set; }
        public string RepoSetName { get; set; }
        public string[] RepoSetNames { get; set; }
        public int TotalIssues { get; set; }
        public object WorkingIssues { get; set; }
        public object UntriagedIssues { get; set; }
        public RepoDefinition[] ReposIncluded { get; set; }

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

    public class GroupByAssigneeViewModel
    {
        public IReadOnlyList<GroupByAssigneeAssignee> Assignees { get; set; }
    }

    public class GroupByAssigneeAssignee
    {
        public string Assignee { get; set; }
        public bool IsInAssociatedPersonSet { get; set; }
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
        public RepoDefinition Repo { get; set; }
        public IReadOnlyList<IssueWithRepo> Issues { get; set; }
    }
}
