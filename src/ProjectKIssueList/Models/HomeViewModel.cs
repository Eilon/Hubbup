using System.Collections.Generic;

namespace ProjectKIssueList.Models
{
    public class HomeViewModel
    {
        public string Name { get; internal set; }
        public IDictionary<string, string[]> RepoSetLists { get; set; }
    }
}
