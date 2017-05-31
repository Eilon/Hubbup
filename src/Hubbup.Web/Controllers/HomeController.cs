using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Hubbup.Web.DataSources;
using Hubbup.Web.Utils;
using Hubbup.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hubbup.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDataSource _dataSource;

        public HomeController(IDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        [Route("")]
        [Authorize]
        public IActionResult Index()
        {
            var repoDataSet = _dataSource.GetRepoDataSet();
            return View(new HomeViewModel
            {
                RepoSetLists = repoDataSet.GetRepoSetLists(),
            });
        }

        [Route("missingrepos")]
        [Authorize]
        public async Task<IActionResult> MissingRepos()
        {
            var gitHubName = HttpContext.User.Identity.Name;
            var gitHubAccessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHubClient = GitHubUtils.GetGitHubClient(gitHubAccessToken);

            var repoDataSet = _dataSource.GetRepoDataSet();

            var repoSetLists = repoDataSet.GetRepoSetLists();
            var distinctOrgs =
                repoSetLists
                    .SelectMany(
                        repoSet => repoSet.Value.Repos.Select(repoDefinition => repoDefinition.Owner))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(org => org).ToList();

            var allOrgRepos = new ConcurrentDictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            var result = AsyncParallelUtils.ForEachAsync(distinctOrgs, 5, async org =>
            {
                var reposInOrg = await gitHubClient.Repository.GetAllForOrg(org);
                allOrgRepos[org] = reposInOrg.Where(repo => !repo.Fork).Select(repo => repo.Name).ToArray();
            });
            await result;

            var missingOrgRepos = allOrgRepos.Select(org =>
                new MissingRepoSet
                {
                    Org = org.Key,
                    MissingRepos =
                        org.Value
                            .Except(
                                repoSetLists
                                    .SelectMany(repoSetList => repoSetList.Value.Repos)
                                    .Select(repoDefinition => repoDefinition.Name), StringComparer.OrdinalIgnoreCase)
                            .OrderBy(repo => repo, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                })
                .OrderBy(missingRepoSet => missingRepoSet.Org, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return View(new MissingReposViewModel
            {
                GitHubUserName = gitHubName,
                RepoSetNames = repoDataSet.GetRepoSetLists().Select(repoSetList => repoSetList.Key).ToArray(),
                MissingRepos = missingOrgRepos,
            });
        }
    }
}
