using System.Collections.Generic;
using System.Threading.Tasks;
using Hubbup.Web.DataSources;
using Hubbup.Web.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Hubbup.Web.Controllers
{
    [Route("miklabel")]
    [Authorize(AuthenticationSchemes = "Cookies")]
    public class MikLabelerController : Controller
    {
        private readonly IDataSource _dataSource;
        private readonly ILogger<MikLabelerController> _logger;

        public MikLabelerController(
            IDataSource dataSource,
            ILogger<MikLabelerController> logger)
        {
            _dataSource = dataSource;
            _logger = logger;
        }

        [Route("")]
        public async Task<IActionResult> Index(string repoSet)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);
            return View(await gitHub.Issue.GetAllForRepository("aspnet", "AspNetCore", new ApiOptions { PageSize = 10, PageCount = 1 }));
        }
    }
}
