using System.Linq;
using Hubbup.Web.DataSources;
using Hubbup.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Hubbup.Web.ViewComponents
{
    public class NavigationBarViewComponent : ViewComponent
    {
        private readonly IDataSource _dataSource;

        public NavigationBarViewComponent(IDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public IViewComponentResult Invoke(string currentGroup = null)
        {
            return View(new NavigationBarViewModel()
            {
                UserName = HttpContext.User.Identity.Name,
                CurrentGroup = currentGroup,
                GroupNames = _dataSource.GetRepoDataSet().GetRepoSetLists().Keys
            });
        }
    }
}
