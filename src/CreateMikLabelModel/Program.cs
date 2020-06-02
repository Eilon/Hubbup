using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Hubbup.MikLabelModel;
using Microsoft.Extensions.Configuration;

namespace CreateMikLabelModel
{
    public class Program
    {
        private static readonly (string owner, string repo)[] Repos = new[]
        {
            ("dotnet", "aspnetcore"),
            ("dotnet", "extensions"),
        };

        static async Task<int> Main()
        {
            foreach (var repo in Repos)
            {
                var tsvRawGitHubDataPath = $"{repo.owner}-{repo.repo}-issueData.tsv";
                var modelOutputDataPath = $"{repo.owner}-{repo.repo}-GitHubLabelerModel.zip";

                var stopWatch = Stopwatch.StartNew();

                using (var outputWriter = new StreamWriter(tsvRawGitHubDataPath))
                {
                    WriteCsvHeader(outputWriter);

                    if (!await ProcessGitHubIssueData(repo.owner, repo.repo, IssueType.Issue, outputWriter))
                    {
                        return -1;
                    }
                    if (!await ProcessGitHubIssueData(repo.owner, repo.repo, IssueType.PullRequest, outputWriter))
                    {
                        return -1;
                    }
                }

                stopWatch.Stop();
                Console.WriteLine($"Done writing TSV in {stopWatch.ElapsedMilliseconds}ms");

                //This line re-trains the ML Model
                MLHelper.BuildAndTrainModel(
                    tsvRawGitHubDataPath,
                    modelOutputDataPath,
                    MyTrainerStrategy.OVAAveragedPerceptronTrainer);

                Console.WriteLine(new string('-', 80));
                Console.WriteLine();
            }

            Console.WriteLine($"Please remember to copy the ZIP files to the web site's ML folder");

            return 0;
        }

        private static async Task<bool> ProcessGitHubIssueData(string owner, string repo, IssueType issueType, StreamWriter outputWriter)
        {
            Console.WriteLine($"Getting all '{issueType}' items for {owner}/{repo}...");

            using (var ghGraphQL = CreateGraphQLClient())
            {
                bool hasNextPage;
                string afterID = null;
                var totalProcessed = 0;
                do
                {
                    var issuePage = await GetGitHubIssuePage(ghGraphQL, owner, repo, issueType, afterID);

                    if (issuePage.IsError)
                    {
                        Console.WriteLine("Error encountered in GraphQL query. Stopping.");
                        return false;
                    }

                    var issuesOfInterest =
                        issuePage.Issues.Repository.Issues.Nodes
                            .Where(i => IsIssueOfInterest(i))
                            .ToList();

                    var uninterestingIssuesWithTooManyLabels =
                        issuePage.Issues.Repository.Issues.Nodes
                            .Except(issuesOfInterest)
                            .Where(i => i.Labels.TotalCount > 10);

                    if (uninterestingIssuesWithTooManyLabels.Any())
                    {
                        // The GraphQL query gets at most 10 labels per issue. So if an issue has more than 10 labels,
                        // but none of the first 10 are an 'area-' label, then it's possible that one of the unseen
                        // labels is an 'area-' label and we don't know about it. So we warn.
                        foreach (var issue in uninterestingIssuesWithTooManyLabels)
                        {
                            Console.WriteLine(
                                $"\tWARNING: Issue {owner}/{repo}#{issue.Number} has more than 10 labels " +
                                $"and the first 10 aren't 'area-' labels so it is ignored.");
                        }
                    }

                    totalProcessed += issuePage.Issues.Repository.Issues.Nodes.Count;
                    Console.WriteLine(
                        $"Processing {totalProcessed}/{issuePage.Issues.Repository.Issues.TotalCount}. " +
                        $"Writing {issuesOfInterest.Count} items of interest to output TSV file...");

                    foreach (var issue in issuesOfInterest)
                    {
                        WriteCsvIssue(outputWriter, issue);
                    }
                    hasNextPage = issuePage.Issues.Repository.Issues.PageInfo.HasNextPage;
                    afterID = issuePage.Issues.Repository.Issues.PageInfo.EndCursor;
                }
                while (hasNextPage);
            }

            return true;
        }

        /// <summary>
        /// Returns 'true' if the issue has at least one 'area-*' label, meaning it can be
        /// used to create training data.
        /// </summary>
        /// <param name="issue"></param>
        /// <returns></returns>
        private static bool IsIssueOfInterest(IssuesNode issue)
        {
            return issue.Labels.Nodes.Any(l => l.Name.StartsWith("area-", StringComparison.OrdinalIgnoreCase));
        }

        private static void WriteCsvHeader(StreamWriter outputWriter)
        {
            outputWriter.WriteLine("ID\tArea\tTitle\tDescription");
        }

        private static void WriteCsvIssue(StreamWriter outputWriter, IssuesNode issue)
        {
            var area = issue.Labels.Nodes.First(l => l.Name.StartsWith("area-", StringComparison.OrdinalIgnoreCase)).Name;
            var body = issue.BodyText.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            outputWriter.WriteLine($"{issue.Number}\t{area}\t{issue.Title}\t{body}");
        }

        private enum IssueType
        {
            Issue,
            PullRequest,
        }

        private static async Task<GitHubIssueListPage> GetGitHubIssuePage(GraphQLHttpClient ghGraphQL, string owner, string repo, IssueType issueType, string afterID)
        {
            var issueNodeName = issueType switch
            {
                IssueType.Issue => "issues", // Query for issues
                IssueType.PullRequest => "issues:pullRequests", // Query for pull requests, but rename the node to 'issues' to re-use code
                _ => throw new ArgumentOutOfRangeException(nameof(issueType)),
            };

            var issueRequest = new GraphQLRequest(
                query: @"query ($owner: String!, $name: String!, $afterIssue: String) {
  repository(owner: $owner, name: $name) {
    name
    " + issueNodeName + @"(after: $afterIssue, first: 100, orderBy: {field: CREATED_AT, direction: DESC}) {
      nodes {
        number
        title
        bodyText
        labels(first: 10) {
          nodes {
            name
          },
          totalCount
        }
      }
      pageInfo {
        hasNextPage
        endCursor
      }
      totalCount
    }
  }
}
",
                variables: new
                {
                    owner = owner,
                    name = repo,
                    afterIssue = afterID,
                });

            var result = await ghGraphQL.SendQueryAsync<Data>(issueRequest);
            if (result.Errors?.Any() ?? false)
            {
                Console.WriteLine($"GraphQL errors! ({result.Errors.Length})");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"\t{error.Message}");
                }
                return new GitHubIssueListPage { IsError = true, };
            }

            var issueList = new GitHubIssueListPage
            {
                Issues = result.Data,
            };

            return issueList;
        }

        private static GraphQLHttpClient CreateGraphQLClient()
        {
            var gitHubAccessToken = GetGitHubAuthToken();

            var graphQLClient = new GraphQLHttpClient("https://api.github.com/graphql", new NewtonsoftJsonSerializer());
            graphQLClient.HttpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    scheme: "bearer",
                    parameter: gitHubAccessToken);
            return graphQLClient;
        }

        private static string GetGitHubAuthToken()
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
            return gitHubAccessToken;
        }
    }
}
