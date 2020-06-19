using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hubbup.Web.Controllers
{
    public class TriageController : Controller
    {
        [Route("/triage/{repoSet}")]
        [Authorize]
        public IActionResult Index(string repoSet)
        {
            return View();
        }
    }
}
