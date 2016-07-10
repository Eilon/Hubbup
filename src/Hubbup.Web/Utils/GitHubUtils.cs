using Octokit;
using Octokit.Internal;
using System.Net.Http;

namespace Hubbup.Web.Utils
{
    public static class GitHubUtils
    {
        public static IGitHubClient GetGitHubClient(string gitHubAccessToken)
        {
            var connection = new Connection(
                new ProductHeaderValue("hubbup.io", Startup.Version),
                new HttpClientAdapter(() => new HttpClientHandler()
                {
                    // MOAR PARALLEL REQUESTS!
                    MaxConnectionsPerServer = 10
                }));
            connection.Credentials = new Credentials(gitHubAccessToken);
            var ghc = new GitHubClient(connection);

            return ghc;
        }
    }
}
