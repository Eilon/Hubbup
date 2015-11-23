using System.Collections.Generic;

namespace ProjectKIssueList.ViewModels
{
    public class MissingRepoSet
    {
        public string Org { get; set; }
        public IList<string> MissingRepos { get; set; }
    }
}
