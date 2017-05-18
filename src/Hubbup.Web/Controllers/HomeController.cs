using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Hubbup.Web.Models;
using Hubbup.Web.Utils;
using Hubbup.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Hubbup.Web.Controllers
{
    public class HomeController : Controller
    {
        public HomeController(IRepoSetProvider repoSetProvider)
        {
            RepoSetProvider = repoSetProvider;
        }

        public IRepoSetProvider RepoSetProvider { get; }

        [Route("")]
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var repoDataSet = await RepoSetProvider.GetRepoDataSet();

            return View(new HomeViewModel
            {
                GitHubUserName = HttpContext.User.Identity.Name,
                RepoSetNames = repoDataSet.GetRepoSetLists().Select(repoSetList => repoSetList.Key).ToArray(),
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

            var repoDataSet = await RepoSetProvider.GetRepoDataSet();

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
