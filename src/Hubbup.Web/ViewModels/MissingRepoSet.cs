using System.Collections.Generic;

namespace Hubbup.Web.ViewModels
{
    public class MissingRepoSet
    {
        public string Org { get; set; }
        public IList<string> MissingRepos { get; set; }
    }
}
