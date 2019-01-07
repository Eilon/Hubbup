using Hubbup.Web.ML;
using Octokit;

namespace Hubbup.Web.ViewModels
{
    public class LabelSuggestion
    {
        public Issue Issue { get; set; }
        public GitHubIssuePrediction Prediction { get; set; }
        public Label AreaLabel { get; set; }
    }
}
