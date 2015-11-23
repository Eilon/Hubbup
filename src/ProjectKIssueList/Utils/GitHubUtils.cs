using Octokit;
using Octokit.Internal;

namespace ProjectKIssueList.Utils
{
    public static class GitHubUtils
    {
        public static GitHubClient GetGitHubClient(string gitHubAccessToken)
        {
            var ghc = new GitHubClient(
                new ProductHeaderValue("Project-K-Issue-List"),
                new InMemoryCredentialStore(new Credentials(gitHubAccessToken)));

            return ghc;
        }
    }
}
