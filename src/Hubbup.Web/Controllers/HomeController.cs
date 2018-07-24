using Hubbup.Web.DataSources;
using Hubbup.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hubbup.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDataSource _dataSource;

        public HomeController(IDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        [Route("")]
        [Authorize]
        public IActionResult Index()
        {
            var repoDataSet = _dataSource.GetRepoDataSet();
            return View(new HomeViewModel
            {
                RepoSetLists = repoDataSet.GetRepoSetLists(),
            });
        }
    }
}
