using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Hubbup.Web.DataSources;
using Hubbup.Web.Utils;
using Hubbup.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
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
