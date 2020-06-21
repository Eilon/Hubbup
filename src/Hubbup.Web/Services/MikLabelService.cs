using Hubbup.MikLabelModel;
using Hubbup.Web.Utils;
using Hubbup.Web.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Hubbup.Web.Services
{
    public class MikLabelService
    {
        private readonly ILogger<MikLabelService> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IMemoryCache _memoryCache;
        private readonly MikLabelerProvider _mikLabelerProvider;
        private static readonly (string owner, string repo)[] Repos = new[]
        {
            ("dotnet", "aspnetcore"),
            ("dotnet", "extensions"),
            //("dotnet", "runtime"),
        };


        public MikLabelService(
            ILogger<MikLabelService> logger,
            IWebHostEnvironment hostingEnvironment,
            IMemoryCache memoryCache,
            MikLabelerProvider mikLabelerProvider)
        {
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
            _memoryCache = memoryCache;
            _mikLabelerProvider = mikLabelerProvider;
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

                var modelPath = Path.Combine("ML", $"{repoIssueResult.Owner}-{repoIssueResult.Repo}-GitHubLabelerModel.zip");
                var prModelPath = Path.Combine("ML", $"{repoIssueResult.Owner}-{repoIssueResult.Repo}-GitHubPrLabelerModel.zip");
                var labeler =
                    _mikLabelerProvider
                        .GetMikLabeler(
                            new MikLabelerStringPathProvider(
                                issuePath: Path.Combine(_hostingEnvironment.ContentRootPath, modelPath),
                                prPath: Path.Combine(_hostingEnvironment.ContentRootPath, prModelPath)))
                        .GetPredictor();

                CachedPrediction prediction;
                foreach (var issue in repoIssueResult.Issues)
                {
                    if (issue.PullRequest == null)
                    {
                        prediction = GetIssuePrediction(repoIssueResult.Owner, repoIssueResult.Repo, issue, labeler);
                    }
                    else
                    {
                        var gitHub = GitHubUtils.GetGitHubClient(accessToken);
                        var prFiles = await gitHub.PullRequest.Files(repoIssueResult.Owner, repoIssueResult.Repo, issue.Number);
                        prediction = GetPrPrediction(repoIssueResult.Owner, repoIssueResult.Repo, issue, prFiles, labeler);
                    }

                    predictionList.Add(new LabelSuggestionViewModel
                    {
                        RepoOwner = repoIssueResult.Owner,
                        RepoName = repoIssueResult.Repo,
                        Issue = issue,
                        LabelScores = prediction.Prediction.LabelScores.Select(ls => (ls, repoIssueResult.AreaLabels.Single(label => string.Equals(label.Name, ls.LabelName, StringComparison.OrdinalIgnoreCase)))).ToList()
                    });
                }
            }

            _logger.LogDebug("Finished label prediction");

            return new MikLabelViewModel
            {
                PredictionList = predictionList.OrderByDescending(prediction => prediction.Issue.CreatedAt).ToList(),
                TotalIssuesFound = totalIssuesFound,
            };

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

        private CachedPrediction GetIssuePrediction(string owner, string repo, Issue issue, MikLabelerPredictor labeler)
        {
            var issueLastModified = issue.UpdatedAt;

            CachedPrediction prediction;
            var predictionCacheKey = $"Predictions/{owner}/{repo}/{issue.Number}";

            _logger.LogTrace("Looking for cached prediction for {ITEM}", predictionCacheKey);

            var cachedPrediction = _memoryCache.GetOrCreate(
                predictionCacheKey,
                cacheEntry =>
                {
                    _logger.LogDebug("[MISS] Creating new cached prediction for {ITEM}", predictionCacheKey);
                    return new CachedPrediction(labeler.PredictLabel(issue), issueLastModified);
                });

            if (issueLastModified > cachedPrediction.IssueLastModified)
            {
                // If the issue has been modified since the cache entry was added,
                // then create a new prediction and cache that
                _logger.LogDebug("[UPDATE] Updating cached prediction for {ITEM}", predictionCacheKey);
                var newPrediction = new CachedPrediction(labeler.PredictLabel(issue), issueLastModified);
                _memoryCache.Set(predictionCacheKey, newPrediction);
                prediction = newPrediction;
            }
            else
            {
                // If the issue has not been modified since the cache entry was added,
                // use the cached prediction
                _logger.LogTrace("[HIT] Using cached prediction for {ITEM}", predictionCacheKey);
                prediction = cachedPrediction;
            }

            return prediction;
        }

        private CachedPrediction GetPrPrediction(string owner, string repo, Issue issue, IReadOnlyList<PullRequestFile> prFiles, MikLabelerPredictor labeler)
        {
            var filePaths = prFiles.Select(x => x.FileName).ToArray();
            var issueLastModified = issue.UpdatedAt;

            CachedPrediction prediction;
            var predictionCacheKey = $"Predictions/{owner}/{repo}/{issue.Number}";

            _logger.LogTrace("Looking for cached prediction for {ITEM}", predictionCacheKey);

            var cachedPrediction = _memoryCache.GetOrCreate(
                predictionCacheKey,
                cacheEntry =>
                {
                    _logger.LogDebug("[MISS] Creating new cached prediction for {ITEM}", predictionCacheKey);
                    return new CachedPrediction(labeler.PredictLabel(issue, filePaths), issueLastModified);
                });

            if (issueLastModified > cachedPrediction.IssueLastModified)
            {
                // If the issue has been modified since the cache entry was added,
                // then create a new prediction and cache that
                _logger.LogDebug("[UPDATE] Updating cached prediction for {ITEM}", predictionCacheKey);
                var newPrediction = new CachedPrediction(labeler.PredictLabel(issue, filePaths), issueLastModified);
                _memoryCache.Set(predictionCacheKey, newPrediction);
                prediction = newPrediction;
            }
            else
            {
                // If the issue has not been modified since the cache entry was added,
                // use the cached prediction
                _logger.LogTrace("[HIT] Using cached prediction for {ITEM}", predictionCacheKey);
                prediction = cachedPrediction;
            }

            return prediction;
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

        private struct CachedPrediction
        {
            public LabelSuggestion Prediction { get; }
            public DateTimeOffset? IssueLastModified { get; }

            public CachedPrediction(LabelSuggestion prediction, DateTimeOffset? issueLastModified) =>
                (Prediction, IssueLastModified) = (prediction, issueLastModified);
        }

        private class RepoIssueResult
        {
            public string Repo { get; set; }
            public string Owner { get; set; }
            public IReadOnlyList<Issue> Issues { get; set; }
            public int TotalCount { get; set; }
            public List<Label> AreaLabels { get; set; }
        }
    }
}
