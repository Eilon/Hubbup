using System.Linq;
using Microsoft.AspNet.Mvc;
using ProjectKIssueList.Models;
using ProjectKIssueList.ViewModels;

namespace ProjectKIssueList.Controllers
{
    public class HomeController : Controller
    {
        public HomeController(IRepoSetProvider repoSetProvider)
        {
            RepoSetProvider = repoSetProvider;
        }

        public IRepoSetProvider RepoSetProvider { get; private set; }

        [Route("")]
        [GitHubAuthData]
        public IActionResult Index(string gitHubName)
        {
            return View(new HomeViewModel
            {
                GitHubUserName = gitHubName,
                RepoSetNames = RepoSetProvider.GetRepoSetLists().Select(repoSetList => repoSetList.Key).ToArray(),
                RepoSetLists = RepoSetProvider.GetRepoSetLists(),
            });
        }
    }
}
