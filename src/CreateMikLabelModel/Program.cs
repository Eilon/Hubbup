using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

        private const int MaxFileChangesPerPR = 100;
        private const string DeletedUser = "ghost";

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

                    if (!await ProcessGitHubIssueData(repo.owner, repo.repo, IssueType.Issue, outputWriter, GetGitHubIssuePage<IssuesNode>))
                    {
                        return -1;
                    }
                    if (!await ProcessGitHubIssueData(repo.owner, repo.repo, IssueType.PullRequest, outputWriter, GetGitHubIssuePage<PullRequestsNode>))
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

        private static async Task<bool> ProcessGitHubIssueData<T>(
            string owner, string repo, IssueType issueType, StreamWriter outputWriter, 
            Func<GraphQLHttpClient, string, string, IssueType, string, Task<GitHubListPage<T>>> getPage) where T : IssuesNode
        {
            Console.WriteLine($"Getting all '{issueType}' items for {owner}/{repo}...");

            using (var ghGraphQL = CreateGraphQLClient())
            {
                bool hasNextPage;
                string afterID = null;
                var totalProcessed = 0;
                do
                {
                    var issuePage = await getPage(ghGraphQL, owner, repo, issueType, afterID);

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

                    if (issueType == IssueType.PullRequest)
                    {
                        var prsWithTooManyFileChanges =
                            issuePage.Issues.Repository.Issues.Nodes
                                .Where(x => x as PullRequestsNode != null).Select(x => x as PullRequestsNode).Where(i => i.Files.TotalCount > MaxFileChangesPerPR);

                        if (prsWithTooManyFileChanges.Any())
                        {
                            // The GraphQL query gets at most N file changes per pr. So if a pr has more than N files changed,
                            // then it's possible that we don't know about it. So we warn.
                            foreach (var issue in prsWithTooManyFileChanges)
                            {
                                Console.WriteLine(
                                    $"\tWARNING: PR {owner}/{repo}#{issue.Number} has more than {MaxFileChangesPerPR} labels ({issue.Files.TotalCount} total)" +
                                    $"and the first {MaxFileChangesPerPR} are only used for training its area.");
                            }
                        }
                    }

                    totalProcessed += issuePage.Issues.Repository.Issues.Nodes.Count;
                    Console.WriteLine(
                        $"Processing {totalProcessed}/{issuePage.Issues.Repository.Issues.TotalCount}. " +
                        $"Writing {issuesOfInterest.Count} items of interest to output TSV file...");

                    foreach (var issue in issuesOfInterest)
                    {
                        WriteCsvIssue(outputWriter, issue, issueType);
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
            outputWriter.WriteLine("ID\tArea\tTitle\tDescription\tAuthor\tIsPR\tFilePaths");
        }

        private static void WriteCsvIssue(StreamWriter outputWriter, IssuesNode issue, IssueType issueType)
        {
            string author = issue.Author != null ? issue.Author.Login : DeletedUser;
            var area = issue.Labels.Nodes.First(l => l.Name.StartsWith("area-", StringComparison.OrdinalIgnoreCase)).Name;
            var body = issue.BodyText.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Replace('"', '`');
            if (issueType == IssueType.Issue)
            {
                outputWriter.WriteLine($"{issue.Number}\t{area}\t{issue.Title}\t{body}\t{author}\t0\t");
            }
            else if (issueType == IssueType.PullRequest && issue is PullRequestsNode pullRequest)
            {
                string filePaths = string.Empty;
                if (pullRequest.Files.Nodes.Count > 0)
                    filePaths = pullRequest.Files.Nodes.Select(x => x.Path)
                        .Aggregate(new StringBuilder(), (a, b) => a.Append(";").Append(b), (a) => a.Remove(0, 1).ToString());
                outputWriter.WriteLine($"{pullRequest.Number}\t{area}\t{pullRequest.Title}\t{body}\t{author}\t1\t{filePaths}");
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(issueType));
            }
        }

        private enum IssueType
        {
            Issue,
            PullRequest,
        }

        private static async Task<GitHubListPage<T>> GetGitHubIssuePage<T>(GraphQLHttpClient ghGraphQL, string owner, string repo, IssueType issueType, string afterID)
        {
            var prSpecific = issueType switch
            {
                IssueType.Issue => string.Empty,
                IssueType.PullRequest => @"files(first: " + MaxFileChangesPerPR + @") {
                    totalCount
                    nodes {
                        path
                    }
                    pageInfo {
                        hasNextPage
                        endCursor
                    }
                }",
                _ => throw new ArgumentOutOfRangeException(nameof(issueType)),
            };
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
        author {
          login
        }" + prSpecific + @"
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

            var result = await ghGraphQL.SendQueryAsync<Data<T>>(issueRequest);
            if (result.Errors?.Any() ?? false)
            {
                Console.WriteLine($"GraphQL errors! ({result.Errors.Length})");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"\t{error.Message}");
                }
                return new GitHubListPage<T> { IsError = true, };
            }

            var issueList = new GitHubListPage<T>
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
