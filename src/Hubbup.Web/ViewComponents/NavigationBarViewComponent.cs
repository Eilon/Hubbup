using Hubbup.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Hubbup.Web.ViewComponents
{
    public class NavigationBarViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(string currentGroup = null)
        {
            return View(new NavigationBarViewModel()
            {
                UserName = HttpContext.User.Identity.Name,
                CurrentGroup = currentGroup,
            });
        }
    }
}
