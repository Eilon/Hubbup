using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Octokit;

namespace Hubbup.MikLabelModel
{
    public class MikLabelerPredictor
    {
        private readonly PredictionEngine<GitHubIssue, GitHubIssuePrediction> _predictionEngine;

        public MikLabelerPredictor(PredictionEngine<GitHubIssue, GitHubIssuePrediction> predictionEngine)
        {
            _predictionEngine = predictionEngine;
        }

        public LabelSuggestion PredictLabel(Issue issue)
        {
            var aspnetIssue = new GitHubIssue
            {
                ID = issue.Number.ToString(),
                Title = issue.Title,
                Description = issue.Body
            };

            var prediction = _predictionEngine.Predict(aspnetIssue);
            var labelPredictions = GetBestThreePredictions(prediction);
            return new LabelSuggestion
            {
                Issue = issue,
                LabelScores = labelPredictions,
            };
        }

        private List<LabelAreaScore> GetBestThreePredictions(GitHubIssuePrediction prediction)
        {
            var scores = prediction.Score;

            VBuffer<ReadOnlyMemory<char>> slotNames = default;
            _predictionEngine.OutputSchema[nameof(GitHubIssuePrediction.Score)].GetSlotNames(ref slotNames);

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
