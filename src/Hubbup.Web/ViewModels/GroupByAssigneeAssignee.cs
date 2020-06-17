using Hubbup.Web.Models;
using System.Collections.Generic;

namespace Hubbup.Web.ViewModels
{
    public class GroupByAssigneeAssignee
    {
        public string Assignee { get; set; }
        public bool IsMetaAssignee { get; set; }
        public bool IsInAssociatedPersonSet { get; set; }
        public IReadOnlyList<IssueWithRepo> Issues { get; set; }
        public IReadOnlyList<IssueWithRepo> OtherIssues { get; set; }
        public IReadOnlyList<PullRequestWithRepo> PullRequests { get; set; }
    }
}
