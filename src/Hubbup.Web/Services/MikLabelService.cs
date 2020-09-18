using Hubbup.Web.Utils;
using Hubbup.Web.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubbup.Web.Services
{
    public class MikLabelService
    {
        private readonly ILogger<MikLabelService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IHttpClientFactory _httpClientFactory;
        private static readonly (string owner, string repo)[] Repos = new[]
        {
            ("dotnet", "aspnetcore"),
            ("dotnet", "extensions"),
            //("dotnet", "runtime"),
        };


        public MikLabelService(
            ILogger<MikLabelService> logger,
            IMemoryCache memoryCache,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _memoryCache = memoryCache;
            _httpClientFactory = httpClientFactory;
        }

        private static string GetPredictionUrl(string owner, string repo, int issueNumber)
        {
            return (owner.ToLowerInvariant(), repo.ToLowerInvariant()) switch
            {
                ("dotnet", "aspnetcore") => string.Format(CultureInfo.InvariantCulture, "https://dotnet-aspnetcore-labeler.azurewebsites.net/api/WebhookIssue/dotnet/aspnetcore/{0}", issueNumber),
                ("dotnet", "extensions") => string.Format(CultureInfo.InvariantCulture, "https://dotnet-extensions-labeler.azurewebsites.net/api/WebhookIssue/dotnet/extensions/{0}", issueNumber),
                _ => throw new ArgumentException($"Can't find remote prediction URL for issue {owner}/{repo}#{issueNumber}."),
            };
        }

        public async Task<MikLabelViewModel> GetViewModel(string accessToken)
        {
            var predictionList = new List<LabelSuggestionViewModel>();
            var totalIssuesFound = 0;

            var repoIssueTasks = new List<Task<RepoIssueResult>>();

            foreach (var (owner, repo) in Repos)
            {
                repoIssueTasks.Add(GetRepoIssues(accessToken, owner, repo));
            }

            var repoIssueResults = await Task.WhenAll(repoIssueTasks);

            _logger.LogDebug("Loaded all issues; starting label prediction...");

            foreach (var repoIssueResult in repoIssueResults)
            {
                totalIssuesFound += repoIssueResult.TotalCount;

                foreach (var issue in repoIssueResult.Issues)
                {
                    await AddIssuePrediction(predictionList, repoIssueResult, issue);
                }
            }

            _logger.LogDebug("Finished label prediction");

            return new MikLabelViewModel
            {
                PredictionList = predictionList.OrderByDescending(prediction => prediction.Issue.CreatedAt).ToList(),
                TotalIssuesFound = totalIssuesFound,
            };

        }

        private async Task AddIssuePrediction(List<LabelSuggestionViewModel> predictionList, RepoIssueResult repoIssueResult, Issue issue)
        {
            var predictionUrl = GetPredictionUrl(repoIssueResult.Owner, repoIssueResult.Repo, issue.Number);
            var request = new HttpRequestMessage(HttpMethod.Get, predictionUrl);
            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                using var responseStream = await response.Content.ReadAsStreamAsync();
                var remotePrediction = await JsonSerializer.DeserializeAsync<RemoteLabelPrediction>(responseStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                predictionList.Add(new LabelSuggestionViewModel
                {
                    RepoOwner = repoIssueResult.Owner,
                    RepoName = repoIssueResult.Repo,
                    Issue = issue,
                    LabelScores = remotePrediction.LabelScores.Select(ls => (new LabelAreaScore { LabelName = ls.LabelName, Score = ls.Score }, repoIssueResult.AreaLabels.Single(label => string.Equals(label.Name, ls.LabelName, StringComparison.OrdinalIgnoreCase)))).ToList()
                });
            }
            else
            {
                predictionList.Add(new LabelSuggestionViewModel
                {
                    RepoOwner = repoIssueResult.Owner,
                    RepoName = repoIssueResult.Repo,
                    Issue = issue,
                    ErrorMessage = $"Could not retrieve label predictions for this issue. Remote HTTP prediction status code {response.StatusCode} from URL '{predictionUrl}'.",
                });
            }
        }

        private async Task<RepoIssueResult> GetRepoIssues(string accessToken, string owner, string repo)
        {
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            _logger.LogDebug("Getting labels for {OWNER}/{REPO}...", owner, repo);
            var existingAreaLabels = await GetAreaLabelsForRepo(gitHub, owner, repo);
            _logger.LogDebug("Got {COUNT} labels for {OWNER}/{REPO}", existingAreaLabels.Count, owner, repo);

            var excludeAllAreaLabelsQuery =
                string.Join(
                    " ",
                    existingAreaLabels.Select(label => $"-label:\"{label.Name}\""));

            var getIssuesRequest = new SearchIssuesRequest($"{excludeAllAreaLabelsQuery} -milestone:Discussions")
            {
                Is = new[] { IssueIsQualifier.Open },
                Repos = new RepositoryCollection
                {
                    { owner, repo }
                },
            };

            _logger.LogDebug("Finding issues for {OWNER}/{REPO}...", owner, repo);
            var searchResults = await gitHub.Search.SearchIssues(getIssuesRequest);
            _logger.LogDebug("Found {COUNT} issues for {OWNER}/{REPO}", searchResults.Items.Count, owner, repo);

            // Trim out results that are hidden due to recent labeling activity
            var trimmedResults = searchResults.Items
                .Where(issue => _memoryCache.Get(GetIssueHiderCacheKey(owner, repo, issue.Number)) == null)
                .ToList()
                .AsReadOnly();

            _logger.LogDebug("Found {COUNT} issues for {OWNER}/{REPO} ({TRIMMED} items trimmed out)", searchResults.Items.Count, owner, repo, searchResults.Items.Count - trimmedResults.Count);


            return new RepoIssueResult
            {
                Owner = owner,
                Repo = repo,
                Issues = trimmedResults,
                TotalCount = searchResults.TotalCount - (searchResults.Items.Count - trimmedResults.Count),
                AreaLabels = existingAreaLabels,
            };
        }

        private async Task<List<Label>> GetAreaLabelsForRepo(IGitHubClient gitHub, string owner, string repo)
        {
            return await _memoryCache.GetOrCreateAsync(
                $"Labels/{owner}/{repo}",
                async cacheEntry =>
                {
                    _logger.LogDebug("Cache MISS for labels for {OWNER}/{REPO}", owner, repo);
                    cacheEntry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
                    return (await gitHub.Issue.Labels.GetAllForRepository(owner, repo))
                        .Where(label => label.Name.StartsWith("area-", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                });
        }

        private static string GetIssueHiderCacheKey(string owner, string repo, int issueNumber) =>
            $"HideIssue/{owner}/{repo}/{issueNumber.ToString(CultureInfo.InvariantCulture)}";

        private class RepoIssueResult
        {
            public string Repo { get; set; }
            public string Owner { get; set; }
            public IReadOnlyList<Issue> Issues { get; set; }
            public int TotalCount { get; set; }
            public List<Label> AreaLabels { get; set; }
        }

        private sealed class RemoteLabelPrediction
        {
            // Meant to deserialize a JSON response like this:
            //{
            //    "labelScores":
            //    [
            //        {
            //            "labelName": "area-infrastructure",
            //            "score": 0.988357544
            //        },
            //        {
            //            "labelName": "area-mvc",
            //            "score": 0.008182112
            //        },
            //        {
            //            "labelName": "area-servers",
            //            "score": 0.002301987
            //        }
            //    ]
            //}
            public List<RemoteLabelPredictionScore> LabelScores { get; set; }

        }

        private sealed class RemoteLabelPredictionScore
        {
            public string LabelName { get; set; }
            public float Score { get; set; }
        }
    }
}
