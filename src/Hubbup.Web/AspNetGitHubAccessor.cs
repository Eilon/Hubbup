using System.Threading.Tasks;
using Hubbup.IssueMoverApi;
using Hubbup.Web.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Octokit;

namespace Hubbup.Web
{
    public class AspNetGitHubAccessor : IGitHubAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AspNetGitHubAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IGitHubClient> GetGitHubClient()
        {
            var accessToken = await _httpContextAccessor.HttpContext.GetTokenAsync("access_token");
            return GitHubUtils.GetGitHubClient(accessToken);
        }
    }
}
