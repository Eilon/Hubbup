using System.Collections.Generic;

namespace ProjectKIssueList.ViewModels
{
    public class GroupByRepoViewModel
    {
        public IReadOnlyList<GroupByRepoRepo> Repos { get; set; }
    }
}
