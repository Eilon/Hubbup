using System.Threading.Tasks;
using Octokit;

namespace Hubbup.IssueMoverApi
{
    public interface IGitHubAccessor
    {
        Task<IGitHubClient> GetGitHubClient();
    }
}
