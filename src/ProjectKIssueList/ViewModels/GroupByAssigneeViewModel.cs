using System.Collections.Generic;

namespace ProjectKIssueList.ViewModels
{
    public class GroupByAssigneeViewModel
    {
        public IReadOnlyList<GroupByAssigneeAssignee> Assignees { get; set; }
    }
}
