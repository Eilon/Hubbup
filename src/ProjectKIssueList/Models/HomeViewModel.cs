using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectKIssueList.Models
{
    public class IssueWithRepo
    {
        public Issue Issue { get; set; }
        public string RepoName { get; set; }
    }

    public class HomeViewModel
    {
        public int TotalIssues { get; set; }
        public GroupByAssigneeViewModel GroupByAssignee { get; set; }
        public GroupByMilestoneViewModel GroupByMilestone { get; set; }
        public GroupByRepoViewModel GroupByRepo { get; set; }
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
