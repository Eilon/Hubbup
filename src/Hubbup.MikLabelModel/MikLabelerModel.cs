using Microsoft.ML;

namespace Hubbup.MikLabelModel
{
    //This "Labeler" class could be used in a different End-User application (Web app, other console app, desktop app, etc.)
    public class MikLabelerModel
    {
        private readonly string _modelPath;
        private readonly MLContext _mlContext;
        private readonly ITransformer _trainedModel;

        public MikLabelerModel(string modelPath)
        {
            _modelPath = modelPath;
            _mlContext = new MLContext(seed: 1);

            // Load model from file
            _trainedModel = _mlContext.Model.Load(_modelPath, inputSchema: out _);
        }

        public MikLabelerPredictor GetPredictor()
        {
            // Create prediction engine related to the loaded trained model
            return new MikLabelerPredictor(_mlContext.Model.CreatePredictionEngine<GitHubIssue, GitHubIssuePrediction>(_trainedModel));
        }
    }
}
