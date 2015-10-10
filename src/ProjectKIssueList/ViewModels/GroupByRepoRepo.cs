using System.Collections.Generic;
using ProjectKIssueList.Models;

namespace ProjectKIssueList.ViewModels
{
    public class GroupByRepoRepo
    {
        public RepoDefinition Repo { get; set; }
        public IReadOnlyList<IssueWithRepo> Issues { get; set; }
    }
}
