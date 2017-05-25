using System.Threading.Tasks;
using Hubbup.Web.DataSources;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Hubbup.Web.Controllers
{
    [Route("groups/{groupName}")]
    public class GroupsController : Controller
    {
        private readonly IDataSource _dataSource;
        private readonly IGitHubDataSource _github;

        public GroupsController(IDataSource dataSource, IGitHubDataSource github)
        {
            _dataSource = dataSource;
            _github = github;
        }

        [Route("issues/{userName}")]
        public async Task<IActionResult> GetIssuesAsync(string groupName, string userName)
        {
            var repoSet = _dataSource.GetRepoDataSet().GetRepoSet(groupName);
            var query = repoSet.BaseQuery + $" assignee:{userName}";
            var results = await _github.SearchIssuesAsync(query, await HttpContext.GetTokenAsync("access_token"));

            return Json(results);
        }
    }
}
