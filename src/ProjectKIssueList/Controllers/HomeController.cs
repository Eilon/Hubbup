using Microsoft.AspNet.Mvc;
using ProjectKIssueList.Models;

namespace ProjectKIssueList.Controllers
{
    public class HomeController : Controller
    {
        [Route("")]
        [GitHubAuthData]
        public IActionResult Index(string gitHubName)
        {
            return View(new HomeViewModel
            {
                Name = gitHubName,
                RepoSetLists = RepoSets.GetRepoSetLists(),
            });
        }
    }
}
