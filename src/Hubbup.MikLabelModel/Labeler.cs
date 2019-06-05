using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Octokit;

namespace Hubbup.MikLabelModel
{
    //This "Labeler" class could be used in a different End-User application (Web app, other console app, desktop app, etc.)
    public class Labeler
    {
        private readonly string _modelPath;
        private readonly MLContext _mlContext;
        private readonly PredictionEngine<GitHubIssue, GitHubIssuePrediction> _predEngine;
        private readonly ITransformer _trainedModel;

        public Labeler(string modelPath)
        {
            _modelPath = modelPath;
            _mlContext = new MLContext(seed: 1);

            // Load model from file
            _trainedModel = _mlContext.Model.Load(_modelPath, inputSchema: out _);

            // Create prediction engine related to the loaded trained model
            _predEngine = _mlContext.Model.CreatePredictionEngine<GitHubIssue, GitHubIssuePrediction>(_trainedModel);
        }

        public LabelSuggestion PredictLabel(Issue issue)
        {
            var aspnetIssue = new GitHubIssue
            {
                ID = issue.Number.ToString(),
                Title = issue.Title,
                Description = issue.Body
            };

            var prediction = _predEngine.Predict(aspnetIssue);
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
            _predEngine.OutputSchema[nameof(GitHubIssuePrediction.Score)].GetSlotNames(ref slotNames);

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
            public IndexedScore(int index, float score)
            {
                Index = index;
                Score = score;
            }

            public int Index { get; }
            public float Score { get; }
        }
    }
}
