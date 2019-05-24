using System.Threading.Tasks;
using Hubbup.IssueMoverApi;
using Hubbup.Web.Utils;
using Microsoft.JSInterop;
using Octokit;

namespace Hubbup.Web
{
    public class AspNetGitHubAccessor : IGitHubAccessor
    {
        private readonly IJSRuntime _jsRuntime;

        public AspNetGitHubAccessor(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<IGitHubClient> GetGitHubClient()
        {
            var accessToken =  await _jsRuntime.InvokeAsync<string>("GetGitHubAccessToken");
            return GitHubUtils.GetGitHubClient(accessToken);
        }
    }
}
