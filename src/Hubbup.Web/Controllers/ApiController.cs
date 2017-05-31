using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hubbup.Web.DataSources;
using Hubbup.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Hubbup.Web.Controllers
{
    [Route("api")]
    public class ApiController : Controller
    {
        private readonly IDataSource _dataSource;
        private readonly IGitHubDataSource _github;

        public ApiController(IDataSource dataSource, IGitHubDataSource github)
        {
            _dataSource = dataSource;
            _github = github;
        }

        [Route("groups/{groupName}/issues/{userName}")]
        public async Task<IActionResult> GetIssuesAsync(string groupName, string userName)
        {
            var repoSet = _dataSource.GetRepoDataSet().GetRepoSet(groupName);
            var query = repoSet.BaseQuery + $" assignee:{userName}";
            var results = await _github.SearchIssuesAsync(query, await HttpContext.GetTokenAsync("access_token"));

            // Identify issues being worked on
            var workingIssues = new List<IssueData>();
            var otherIssues = new List<IssueData>();
            foreach (var result in results)
            {
                if (result.Labels.Any(l => repoSet.WorkingLabels.Contains(l.Name)))
                {
                    workingIssues.Add(result);
                }
                else
                {
                    otherIssues.Add(result);
                }
            }

            return Json(new
            {
                working = workingIssues,
                other = otherIssues
            });
        }
    }
}
