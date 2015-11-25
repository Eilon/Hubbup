using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using ProjectKIssueList.Models;
using ProjectKIssueList.Utils;
using ProjectKIssueList.ViewModels;

namespace ProjectKIssueList.Controllers
{
    public class HomeController : Controller
    {
        public HomeController(IRepoSetProvider repoSetProvider)
        {
            RepoSetProvider = repoSetProvider;
        }

        public IRepoSetProvider RepoSetProvider { get; }

        [Route("")]
        [GitHubAuthData]
        public IActionResult Index(string gitHubName)
        {
            return View(new HomeViewModel
            {
                GitHubUserName = gitHubName,
                RepoSetNames = RepoSetProvider.GetRepoSetLists().Select(repoSetList => repoSetList.Key).ToArray(),
                RepoSetLists = RepoSetProvider.GetRepoSetLists(),
            });
        }

        [Route("missingrepos")]
        [GitHubAuthData]
        public async Task<IActionResult> MissingRepos(string gitHubAccessToken, string gitHubName)
        {
            var gitHubClient = GitHubUtils.GetGitHubClient(gitHubAccessToken);

            var repoSetLists = RepoSetProvider.GetRepoSetLists();
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
                RepoSetNames = RepoSetProvider.GetRepoSetLists().Select(repoSetList => repoSetList.Key).ToArray(),
                MissingRepos = missingOrgRepos,
            });
        }
    }
}
