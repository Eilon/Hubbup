using System.Collections.Generic;

namespace Hubbup.Web.ViewModels
{
    public class NavigationBarViewModel
    {
        public string UserName { get; set;  }
        public string CurrentGroup { get; set; }
        public ICollection<string> GroupNames { get; set; }
    }
}
