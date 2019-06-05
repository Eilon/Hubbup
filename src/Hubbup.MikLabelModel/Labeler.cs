using System;
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

            //Load model from file
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
            var size = scores.Length;

            VBuffer<ReadOnlyMemory<char>> slotNames = default;
            _predEngine.OutputSchema[nameof(GitHubIssuePrediction.Score)].GetSlotNames(ref slotNames);

            GetIndexesOfTopThreeScores(scores, size, out var index0, out var index1, out var index2);

            return new List<LabelAreaScore>
                {
                    new LabelAreaScore {LabelName=slotNames.GetItemOrDefault(index0).ToString(), Score = scores[index0] },
                    new LabelAreaScore {LabelName=slotNames.GetItemOrDefault(index1).ToString(), Score = scores[index1] },
                    new LabelAreaScore {LabelName=slotNames.GetItemOrDefault(index2).ToString(), Score = scores[index2] },
                };
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
                {
                    third = scores[i];
                }
            }
            var scoresList = scores.ToList();
            index0 = scoresList.IndexOf(first);
            index1 = scoresList.IndexOf(second);
            index2 = scoresList.IndexOf(third);
        }
    }
}
