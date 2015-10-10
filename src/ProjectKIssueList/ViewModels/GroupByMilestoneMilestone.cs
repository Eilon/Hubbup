using System.Collections.Generic;
using ProjectKIssueList.Models;

namespace ProjectKIssueList.ViewModels
{
    public class GroupByMilestoneMilestone
    {
        public string Milestone { get; set; }
        public IReadOnlyList<IssueWithRepo> Issues { get; set; }
    }
}
