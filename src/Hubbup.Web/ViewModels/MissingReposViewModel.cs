using System.Collections.Generic;

namespace Hubbup.Web.ViewModels
{
    public class MissingReposViewModel
    {
        public string GitHubUserName { get; set; }
        public string[] RepoSetNames { get; set; }
        public IList<MissingRepoSet> MissingRepos { get; set; }
    }
}
