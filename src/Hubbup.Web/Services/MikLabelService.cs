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
using System.Threading;
using System.Threading.Tasks;

namespace Hubbup.Web.Services
{
    public class MikLabelService
    {
        private readonly ILogger<MikLabelService> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IHttpClientFactory _httpClientFactory;


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
                ("dotnet", "extensions") => string.Format(CultureInfo.InvariantCulture, "https://dotnet-runtime-issue-labeler.azurewebsites.net/api/WebhookIssue/dotnet/Extensions/{0}", issueNumber),
                ("dotnet", "maui") => string.Format(CultureInfo.InvariantCulture, "https://dotnet-aspnetcore-labeler.azurewebsites.net/api/WebhookIssue/dotnet/maui/{0}", issueNumber),
                _ => throw new ArgumentException($"Can't find remote prediction URL for issue {owner}/{repo}#{issueNumber}."),
            };
        }

        public async Task<MikLabelViewModel> GetViewModel(string accessToken, RepoSet repoSet)
        {
            var predictionList = new List<LabelSuggestionViewModel>();
            var totalIssuesFound = 0;

            var repoIssueTasks = new List<Task<RepoIssueResult>>();

            foreach (var (owner, repo) in repoSet.Repos)
            {
                repoIssueTasks.Add(GetRepoIssues(accessToken, owner, repo));
            }

            var repoIssueResults = await Task.WhenAll(repoIssueTasks);

            _logger.LogDebug("Loaded all issues; starting label prediction...");

            var allAreaLabelsForRepoSet = new List<Label>();

            foreach (var repoIssueResult in repoIssueResults)
            {
                totalIssuesFound += repoIssueResult.TotalCount;

                foreach (var issue in repoIssueResult.Issues)
                {
                    await AddIssuePrediction(predictionList, repoIssueResult, issue);
                }

                allAreaLabelsForRepoSet.AddRange(repoIssueResult.AreaLabels);
            }

            _logger.LogDebug("Finished label prediction");

            return new MikLabelViewModel
            {
                PredictionList = predictionList.OrderByDescending(prediction => prediction.Issue.CreatedAt).ToList(),
                TotalIssuesFound = totalIssuesFound,
                AllAreaLabels =
                    allAreaLabelsForRepoSet
                        .OrderBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
            };

        }

        private struct CachedPrediction
        {
            public LabelSuggestion Prediction { get; }
            public DateTimeOffset? IssueLastModified { get; }

            public CachedPrediction(LabelSuggestion prediction, DateTimeOffset? issueLastModified) =>
                (Prediction, IssueLastModified) = (prediction, issueLastModified);
        }


        private async Task AddIssuePrediction(List<LabelSuggestionViewModel> predictionList, RepoIssueResult repoIssueResult, Issue issue)
        {
            // Check cache entry for previous successful prediction
            var issueLastModified = issue.UpdatedAt;

            var predictionCacheKey = $"Predictions/{repoIssueResult.Owner}/{repoIssueResult.Repo}/{issue.Number}";

            _logger.LogTrace("Looking for cached prediction for {ITEM}", predictionCacheKey);

            if (_memoryCache.TryGetValue(
                predictionCacheKey,
                out CachedPrediction cachedPrediction))
            {

                // if cache entry found, check that it isn't out-of-date compared to the issue
                if (issueLastModified <= cachedPrediction.IssueLastModified)
                {
                    // If the issue has not been modified since the cache entry was added,
                    // use the cached prediction
                    _logger.LogTrace("[HIT] Using cached prediction for {ITEM}", predictionCacheKey);

                    AddPredictionToList(predictionList, repoIssueResult, issue, cachedPrediction.Prediction.LabelScores);

                    return;
                }

                // If the issue has been modified since the cache entry was added,
                // then go create a new prediction and cache that
                _logger.LogDebug("[UPDATE] Updating stale cached prediction for {ITEM}", predictionCacheKey);
            }

            var predictionUrl = GetPredictionUrl(repoIssueResult.Owner, repoIssueResult.Repo, issue.Number);
            var request = new HttpRequestMessage(HttpMethod.Get, predictionUrl);
            var client = _httpClientFactory.CreateClient();
            var responseTimeOutSeconds = 5;
            var cts = new CancellationTokenSource(responseTimeOutSeconds * 1_000);
            HttpResponseMessage response = null;
            Exception failureException = null;
            try
            {
                response = await client.SendAsync(request, cts.Token);
            }
            catch (Exception ex)
            {
                failureException = ex;
            }

            if (response != null && response.IsSuccessStatusCode)
            {
                using var responseStream = await response.Content.ReadAsStreamAsync();
                var remotePrediction = await JsonSerializer.DeserializeAsync<RemoteLabelPrediction>(responseStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var areaScores = remotePrediction.LabelScores.Select(remoteScore => new LabelAreaScore { LabelName = remoteScore.LabelName, Score = remoteScore.Score, }).ToList();
                AddPredictionToList(predictionList, repoIssueResult, issue, areaScores);

                // cache successful prediction
                var newPrediction = new CachedPrediction(new LabelSuggestion { LabelScores = areaScores }, issue.UpdatedAt);
                _memoryCache.Set(predictionCacheKey, newPrediction);

                _logger.LogDebug("[CACHE] Storing cached prediction for {OWNER}/{REPO}#{NUMBER}", repoIssueResult.Owner, repoIssueResult.Repo, issue.Number);
            }
            else
            {
                string failureReason;
                if (cts.IsCancellationRequested)
                {
                    failureReason = $"Prediction response from URL '{predictionUrl}' timed out after {responseTimeOutSeconds} seconds.";
                }
                else if (response != null)
                {
                    failureReason = $"Remote HTTP prediction status code {response.StatusCode} from URL '{predictionUrl}'.";
                }
                else
                {
                    failureReason = $"Unexpected exception: {failureException?.Message ?? "<null>"}";
                }
                predictionList.Add(new LabelSuggestionViewModel
                {
                    RepoOwner = repoIssueResult.Owner,
                    RepoName = repoIssueResult.Repo,
                    Issue = issue,
                    ErrorMessage = $"Could not retrieve label predictions for this issue. {failureReason}",
                });

                _logger.LogWarning("Failed to get prediction for {ITEM}", predictionCacheKey);

                // don't cache failed predictions (the prediction API call can fail for many reasons, so we want to give it another chance)
            }
        }

        private static void AddPredictionToList(List<LabelSuggestionViewModel> predictionList, RepoIssueResult repoIssueResult, Issue issue, List<LabelAreaScore> areaScores)
        {
            predictionList.Add(new LabelSuggestionViewModel
            {
                RepoOwner = repoIssueResult.Owner,
                RepoName = repoIssueResult.Repo,
                Issue = issue,
                LabelScores =
                                    areaScores
                                        .Select(ls =>
                                            (
                                                new LabelAreaScore
                                                {
                                                    LabelName = ls.LabelName,
                                                    Score = ls.Score,
                                                },
                                                repoIssueResult.AreaLabels
                                                    .SingleOrDefault(label => string.Equals(label.Name, ls.LabelName, StringComparison.OrdinalIgnoreCase))
                                            ))
                                            .ToList()
            });
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
                PerPage = 50, // Note: 100 is the max issues per page that is supported by GitHub
            };

            _logger.LogDebug("Finding issues for {OWNER}/{REPO}...", owner, repo);
            var searchResults = await gitHub.Search.SearchIssues(getIssuesRequest);
            _logger.LogDebug("Found {COUNT} issues for {OWNER}/{REPO}", searchResults.Items.Count, owner, repo);

            // Trim out results that are hidden due to recent labeling activity
            var trimmedResults = searchResults.Items
                .Where(issue => _memoryCache.Get(GetIssueHiderCacheKey(owner, repo, issue.Number)) == null)
                .ToList()
                .AsReadOnly();

            // Trim out results that are erroneously returned by GitHub's search API. It sometimes returns
            // results that _do_ have an area-XYZ label even though the search query excludes them, so we
            // remove any issues that have area-XYZ labels.
            trimmedResults = trimmedResults
                .Where(i => !IssueHasAreaLabel(i))
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

        private static bool IssueHasAreaLabel(Issue issue)
        {
            return
                issue.Labels.Any(
                    label =>
                        IsAreaLabel(label.Name));
        }

        private static bool IsAreaLabel(string labelName) =>
            labelName.StartsWith("area-", StringComparison.OrdinalIgnoreCase) ||
            labelName.StartsWith("area/", StringComparison.OrdinalIgnoreCase);

        private async Task<List<Label>> GetAreaLabelsForRepo(IGitHubClient gitHub, string owner, string repo)
        {
            return await _memoryCache.GetOrCreateAsync(
                $"Labels/{owner}/{repo}",
                async cacheEntry =>
                {
                    _logger.LogDebug("Cache MISS for labels for {OWNER}/{REPO}", owner, repo);
                    cacheEntry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
                    return (await gitHub.Issue.Labels.GetAllForRepository(owner, repo))
                        .Where(label => IsAreaLabel(label.Name))
                        .OrderBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                });
        }

        internal static string GetIssueHiderCacheKey(string owner, string repo, int issueNumber) =>
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
