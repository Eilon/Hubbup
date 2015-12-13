using System.Collections.Generic;
using Hubbup.Web.Models;

namespace Hubbup.Web.ViewModels
{
    public class HomeViewModel
    {
        public string GitHubUserName { get; set; }
        public string[] RepoSetNames { get; set; }
        public IDictionary<string, RepoSetDefinition> RepoSetLists { get; set; }
    }
}
