using System.Collections.Generic;
using Hubbup.Web.Models;

namespace Hubbup.Web.ViewModels
{
    public class GroupByMilestoneMilestone
    {
        public string Milestone { get; set; }
        public IReadOnlyList<IssueWithRepo> Issues { get; set; }
    }
}
