using System.Collections.Generic;
using Hubbup.Web.Models;

namespace Hubbup.Web.ViewModels
{
    public class GroupByAssigneeViewModel
    {
        public IReadOnlyList<GroupByAssigneeAssignee> Assignees { get; set; }
    }
}
