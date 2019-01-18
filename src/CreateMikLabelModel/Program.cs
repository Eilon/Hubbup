using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Hubbup.MikLabelModel;
using Microsoft.Extensions.Configuration;
using Octokit;

namespace CreateMikLabelModel
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var tsvRawGitHubDataPath = "issueData.tsv";
            var modelOutputDataPath = "GitHubLabelerModel.zip";

            await GetGitHubIssueData(outputPath: tsvRawGitHubDataPath);

            //This line re-trains the ML Model
            MLHelper.BuildAndTrainModel(
                tsvRawGitHubDataPath,
                modelOutputDataPath,
                MyTrainerStrategy.OVAAveragedPerceptronTrainer);

            Console.WriteLine($"Please remember to copy {modelOutputDataPath} to the web site's ML folder");
        }

        private static async Task GetGitHubIssueData(string outputPath)
        {
            Console.WriteLine($"Getting all issues...");

            var stopWatch = Stopwatch.StartNew();

            // Get credentials
            var ghc = GitHubClientFactory.Create() as GitHubClient;

            var allIssues = new List<(string owner, string repo, Issue issue)>();

            var allIssuesInRepo = await ghc.Issue.GetAllForRepository(
                "aspnet",
                "AspNetCore",
                new RepositoryIssueRequest
                {
                    State = ItemStateFilter.All,
                    SortProperty = IssueSort.Created,
                    SortDirection = SortDirection.Descending,
                });

            Console.WriteLine($"Found {allIssuesInRepo.Count} total issues");

            var issuesOfInterest =
                allIssuesInRepo
                    .Where(i => i.Labels.Any(l => l.Name.StartsWith("area-", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

            Console.WriteLine($"Found {issuesOfInterest.Count} of interest (have at least 1 area label)");

            Console.WriteLine($"Writing to output TSV file {outputPath}...");

            using (var outputWriter = new StreamWriter(outputPath))
            {
                outputWriter.WriteLine("ID\tArea\tTitle\tDescription");
                foreach (var issue in issuesOfInterest)
                {
                    var area = issue.Labels.First(l => l.Name.StartsWith("area-", StringComparison.OrdinalIgnoreCase)).Name;
                    var body = issue.Body.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
                    outputWriter.WriteLine($"{issue.Number}\t{area}\t{issue.Title}\t{body}");
                }
            }

            stopWatch.Stop();
            Console.WriteLine($"Done writing TSV in {stopWatch.ElapsedMilliseconds}ms");
        }

        private static class GitHubClientFactory
        {
            private static readonly string Version = typeof(GitHubClientFactory).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            public static IGitHubClient Create()
            {
                const string UserSecretKey = "GitHubAccessToken";

                var config = new ConfigurationBuilder()
                    .AddUserSecrets("AspNetHello.App")
                    .Build();
                var gitHubAccessToken = config[UserSecretKey];
                if (string.IsNullOrEmpty(gitHubAccessToken))
                {
                    throw new InvalidOperationException($"Couldn't find User Secret named '{UserSecretKey}' in configuration.");
                }

                var connection = new Connection(new ProductHeaderValue("AspNetHello.GitHubUtils", Version))
                {
                    Credentials = new Credentials(gitHubAccessToken)
                };
                var ghc = new GitHubClient(connection);

                return ghc;
            }
        }
    }
}
