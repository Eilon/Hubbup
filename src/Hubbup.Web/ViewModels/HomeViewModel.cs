using Hubbup.Web.Models;
using System.Collections.Generic;

namespace Hubbup.Web.ViewModels
{
    public class HomeViewModel
    {
        public IDictionary<string, RepoSetDefinition> RepoSetLists { get; set; }
    }
}
