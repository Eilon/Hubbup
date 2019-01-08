using Microsoft.ML.Runtime.Api;

#pragma warning disable 649 // We don't care about unsused fields here, because they are mapped with the input file.

namespace Hubbup.MikLabelModel
{
    public class GitHubIssuePrediction
    {
        [ColumnName("PredictedLabel")]
        public string Area;

        [ColumnName("Score")]
        public float[] Score { get; set; }
    }
}
