using System.Collections.Generic;
using Hubbup.Web.DataSources;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hubbup.Web.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IDataSource _dataSource;

        [BindProperty(SupportsGet = true)]
        public string GroupName { get; set; }

        public IReadOnlyList<string> People { get; set; }

        public IndexModel(IDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public void OnGet()
        {
            // Load all the people in this group
            var group = _dataSource.GetRepoDataSet().GetRepoSet(GroupName);
            var people = _dataSource.GetPersonSet(group.AssociatedPersonSetName);

            People = people.People;
        }
    }
}
