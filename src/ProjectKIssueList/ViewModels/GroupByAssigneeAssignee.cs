using System.Collections.Generic;
using ProjectKIssueList.Models;

namespace ProjectKIssueList.ViewModels
{
    public class GroupByAssigneeAssignee
    {
        public string Assignee { get; set; }
        public bool IsInAssociatedPersonSet { get; set; }
        public IReadOnlyList<IssueWithRepo> Issues { get; set; }
        public IReadOnlyList<IssueWithRepo> OtherIssues { get; set; }
    }
}
