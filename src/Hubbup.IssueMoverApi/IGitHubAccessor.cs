using Octokit;
using System.Threading.Tasks;

namespace Hubbup.IssueMoverApi
{
    public interface IGitHubAccessor
    {
        Task<IGitHubClient> GetGitHubClient();
    }
}
