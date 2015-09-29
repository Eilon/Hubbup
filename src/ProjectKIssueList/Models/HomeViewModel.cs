using System.Collections.Generic;

namespace ProjectKIssueList.Models
{
    public class HomeViewModel
    {
        public string GitHubUserName { get; set; }
        public string[] RepoSetNames { get; set; }
        public IDictionary<string, RepoSetDefinition> RepoSetLists { get; set; }
    }
}
