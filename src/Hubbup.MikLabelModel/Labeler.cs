using System;
using System.IO;
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

        private FullPrediction[] _fullPredictions;

        public Labeler(string modelPath)
        {
            _modelPath = modelPath;
            _mlContext = new MLContext(seed: 1);

            //Load model from file
            _trainedModel = _mlContext.Model.Load(_modelPath, out _);

            // Create prediction engine related to the loaded trained model
            _predEngine = _mlContext.Model.CreatePredictionEngine<GitHubIssue, GitHubIssuePrediction>(_trainedModel);
        }

        public FullPrediction[] PredictLabel(Issue issue)
        {
            var aspnetIssue = new GitHubIssue
            {
                ID = issue.Number.ToString(),
                Title = issue.Title,
                Description = issue.Body
            };

            _fullPredictions = Predict(aspnetIssue);

            return _fullPredictions;
        }

        public FullPrediction[] Predict(GitHubIssue issue)
        {
            var prediction = _predEngine.Predict(issue);
            var fullPredictions = GetBestThreePredictions(prediction);
            return fullPredictions;
        }

        private FullPrediction[] GetBestThreePredictions(GitHubIssuePrediction prediction)
        {
            var scores = prediction.Score;
            var size = scores.Length;

            VBuffer<ReadOnlyMemory<char>> slotNames = default;
            _predEngine.OutputSchema[nameof(GitHubIssuePrediction.Score)].GetSlotNames(ref slotNames);

            GetIndexesOfTopThreeScores(scores, size, out var index0, out var index1, out var index2);

            _fullPredictions = new FullPrediction[]
                {
                    new FullPrediction(slotNames.GetItemOrDefault(index0).ToString(), scores[index0], index0),
                    new FullPrediction(slotNames.GetItemOrDefault(index1).ToString(), scores[index1], index1),
                    new FullPrediction(slotNames.GetItemOrDefault(index2).ToString(), scores[index2], index2),
                };

            return _fullPredictions;
        }

        private void GetIndexesOfTopThreeScores(float[] scores, int n, out int index0, out int index1, out int index2)
        {
            int i;
            float first, second, third;
            index0 = index1 = index2 = 0;
            if (n < 3)
            {
                Console.WriteLine("Invalid Input");
                return;
            }
            third = first = second = 000;
            for (i = 0; i < n; i++)
            {
                // If current element is  
                // smaller than first 
                if (scores[i] > first)
                {
                    third = second;
                    second = first;
                    first = scores[i];
                }
                // If arr[i] is in between first 
                // and second then update second 
                else if (scores[i] > second)
                {
                    third = second;
                    second = scores[i];
                }

                else if (scores[i] > third)
                    third = scores[i];
            }
            var scoresList = scores.ToList();
            index0 = scoresList.IndexOf(first);
            index1 = scoresList.IndexOf(second);
            index2 = scoresList.IndexOf(third);
        }
    }

    public class FullPrediction
    {
        public string PredictedLabel;
        public float Score;
        public int OriginalSchemaIndex;

        public FullPrediction(string predictedLabel, float score, int originalSchemaIndex)
        {
            PredictedLabel = predictedLabel;
            Score = score;
            OriginalSchemaIndex = originalSchemaIndex;
        }
    }
}