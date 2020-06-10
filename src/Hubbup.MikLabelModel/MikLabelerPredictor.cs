using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.ML;
using Microsoft.ML.Data;
using Octokit;

namespace Hubbup.MikLabelModel
{
    public class MikLabelerPredictor
    {
        private readonly PredictionEngine<GitHubIssue, GitHubIssuePrediction> _predictionEngine;
        private readonly PredictionEngine<GitHubPullRequest, GitHubIssuePrediction> _prPredictionEngine;

        public MikLabelerPredictor(PredictionEngine<GitHubIssue, GitHubIssuePrediction> predictionEngine,
            PredictionEngine<GitHubPullRequest, GitHubIssuePrediction> prPredictionEngine)
        {
            _predictionEngine = predictionEngine;
            _prPredictionEngine = prPredictionEngine;
        }

        private Regex _regex;

        public LabelSuggestion PredictLabel(Issue issue)
        {
            if (_regex == null)
            {
                _regex = new Regex(@"@[a-zA-Z0-9_//-]+");
            }
            var userMentions = _regex.Matches(issue.Body).Select(x => x.Value).ToArray();

            var aspnetIssue = new GitHubIssue
            {
                ID = issue.Number,
                Title = issue.Title,
                Description = issue.Body,
                IsPR = 0,
                Author = issue.User.Login,
                UserMentions = string.Join(' ', userMentions),
                NumMentions = userMentions.Length,
            };

            var prediction = _predictionEngine.Predict(aspnetIssue);
            var labelPredictions = GetBestThreePredictions(prediction, forPrs: false);
            return new LabelSuggestion
            {
                LabelScores = labelPredictions,
            };
        }

        public LabelSuggestion PredictLabel(PullRequest pr, string[] filePaths)
        {
            if (_regex == null)
            {
                _regex = new Regex(@"@[a-zA-Z0-9_//-]+");
            }
            var userMentions = _regex.Matches(pr.Body).Select(x => x.Value).ToArray();

            var diffHelper = new DiffHelper();
            var segmentedDiff = diffHelper.SegmentDiff(filePaths);
            var aspnetIssue = new GitHubPullRequest
            {
                ID = pr.Number,
                Title = pr.Title,
                Description = pr.Body,
                IsPR = 1,
                FileCount = filePaths.Length,
                Files = string.Join(' ', segmentedDiff.FileDiffs),
                Filenames = string.Join(' ', segmentedDiff.Filenames),
                FileExtensions = string.Join(' ', segmentedDiff.Extensions),
                FolderNames = diffHelper.FlattenWithWhitespace(segmentedDiff.FolderNames),
                Folders = diffHelper.FlattenWithWhitespace(segmentedDiff.Folders)
            };

            var prediction = _prPredictionEngine.Predict(aspnetIssue);
            var labelPredictions = GetBestThreePredictions(prediction, forPrs: true);
            return new LabelSuggestion
            {
                LabelScores = labelPredictions,
            };
        }

        private List<LabelAreaScore> GetBestThreePredictions(GitHubIssuePrediction prediction, bool forPrs)
        {
            var scores = prediction.Score;

            VBuffer<ReadOnlyMemory<char>> slotNames = default;
            _predictionEngine.OutputSchema[nameof(GitHubIssuePrediction.Score)].GetSlotNames(ref slotNames);
            if (forPrs)
            {
                _prPredictionEngine.OutputSchema[nameof(GitHubIssuePrediction.Score)].GetSlotNames(ref slotNames);
            }

            var topThreeScores = GetIndexesOfTopScores(scores, 3);

            return new List<LabelAreaScore>
                {
                    new LabelAreaScore {LabelName=slotNames.GetItemOrDefault(topThreeScores[0]).ToString(), Score = scores[topThreeScores[0]] },
                    new LabelAreaScore {LabelName=slotNames.GetItemOrDefault(topThreeScores[1]).ToString(), Score = scores[topThreeScores[1]] },
                    new LabelAreaScore {LabelName=slotNames.GetItemOrDefault(topThreeScores[2]).ToString(), Score = scores[topThreeScores[2]] },
                };
        }

        private IReadOnlyList<int> GetIndexesOfTopScores(float[] scores, int n)
        {
            var indexedScores = scores
                .Zip(Enumerable.Range(0, scores.Length), (score, index) => new IndexedScore(index, score));

            var indexedScoresSortedByScore = indexedScores
                .OrderByDescending(indexedScore => indexedScore.Score);

            return indexedScoresSortedByScore
                .Take(n)
                .Select(indexedScore => indexedScore.Index)
                .ToList()
                .AsReadOnly();
        }

        private struct IndexedScore
        {
            public IndexedScore(int index, float score) => (Index, Score) = (index, score);

            public int Index { get; }
            public float Score { get; }
        }
    }
}
