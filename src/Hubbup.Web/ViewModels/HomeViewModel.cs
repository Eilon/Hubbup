using System.Collections.Generic;
using Hubbup.Web.Models;

namespace Hubbup.Web.ViewModels
{
    public class HomeViewModel
    {
        public IDictionary<string, RepoSetDefinition> RepoSetLists { get; set; }
    }
}
