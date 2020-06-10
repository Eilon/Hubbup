using Microsoft.ML;

namespace Hubbup.MikLabelModel
{
    //This "Labeler" class could be used in a different End-User application (Web app, other console app, desktop app, etc.)
    public class MikLabelerModel
    {
        private readonly string _modelPath;
        private readonly string _prModelPath;
        private readonly MLContext _mlContext;
        private readonly ITransformer _trainedModel;
        private readonly ITransformer _trainedPrModel;

        public MikLabelerModel((string modelPath, string prModelPath) paths)
        {
            _modelPath = paths.modelPath;
            _prModelPath = paths.prModelPath;
            _mlContext = new MLContext(seed: 1);

            // Load model from file
            _trainedModel = _mlContext.Model.Load(_modelPath, inputSchema: out _);
            _trainedPrModel = _mlContext.Model.Load(_prModelPath, inputSchema: out _);
        }

        public MikLabelerPredictor GetPredictor()
        {
            // Create prediction engine related to the loaded trained model
            return new MikLabelerPredictor(
                _mlContext.Model.CreatePredictionEngine<GitHubIssue, GitHubIssuePrediction>(_trainedModel),
                _mlContext.Model.CreatePredictionEngine<GitHubPullRequest, GitHubIssuePrediction>(_trainedPrModel));
        }
    }
}
