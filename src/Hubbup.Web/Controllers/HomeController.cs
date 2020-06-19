using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hubbup.Web.Controllers
{
    public class HomeController : Controller
    {
        [Route("")]
        [Authorize]
        public IActionResult Index()
        {
            return View();
        }
    }
}
