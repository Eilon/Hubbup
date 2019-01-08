using System.IO;
using Microsoft.ML;
using Microsoft.ML.Core.Data;
using Microsoft.ML.Runtime.Data;
using Octokit;

namespace Hubbup.MikLabelModel
{
    //This "Labeler" class could be used in a different End-User application (Web app, other console app, desktop app, etc.)
    public class Labeler
    {
        private readonly string _modelPath;
        private readonly MLContext _mlContext;
        private readonly PredictionFunction<GitHubIssue, GitHubIssuePrediction> _predFunction;
        private readonly ITransformer _trainedModel;

        public Labeler(string modelPath)
        {
            _modelPath = modelPath;
            _mlContext = new MLContext(seed: 1);

            //Load model from file
            using (var stream = File.OpenRead(_modelPath))
            {
                _trainedModel = _mlContext.Model.Load(stream);
            }

            // Create prediction engine related to the loaded trained model
            _predFunction = _trainedModel.MakePredictionFunction<GitHubIssue, GitHubIssuePrediction>(_mlContext);
        }

        public GitHubIssuePrediction PredictLabel(Issue issue)
        {
            var aspnetIssue = new GitHubIssue
            {
                ID = issue.Number.ToString(),
                Title = issue.Title,
                Description = issue.Body
            };

            var predictedLabel = Predict(aspnetIssue);

            return predictedLabel;
        }

        public GitHubIssuePrediction Predict(GitHubIssue issue)
        {
            return _predFunction.Predict(issue);
        }
    }
}