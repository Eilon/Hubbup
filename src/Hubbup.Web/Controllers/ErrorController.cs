﻿using Microsoft.AspNetCore.Mvc;

namespace Hubbup.Web.Controllers
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
