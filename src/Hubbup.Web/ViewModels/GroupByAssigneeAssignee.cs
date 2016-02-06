using System.Collections.Generic;
using Hubbup.Web.Models;

namespace Hubbup.Web.ViewModels
{
    public class GroupByAssigneeAssignee
    {
        public string Assignee { get; set; }
        public bool IsInAssociatedPersonSet { get; set; }
        public IReadOnlyList<IssueWithRepo> Issues { get; set; }
        public IReadOnlyList<IssueWithRepo> OtherIssues { get; set; }
        public IReadOnlyList<PullRequestWithRepo> PullRequests { get; set; }
    }
}
