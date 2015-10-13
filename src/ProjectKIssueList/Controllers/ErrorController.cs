using Microsoft.AspNet.Mvc;

namespace ProjectKIssueList.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
