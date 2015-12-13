using System.Collections.Generic;
using Hubbup.Web.Models;

namespace Hubbup.Web.ViewModels
{
    public class GroupByRepoRepo
    {
        public RepoDefinition Repo { get; set; }
        public IReadOnlyList<IssueWithRepo> Issues { get; set; }
    }
}
