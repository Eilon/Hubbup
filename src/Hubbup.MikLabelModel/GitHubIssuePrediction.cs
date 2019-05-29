using Microsoft.ML.Data;

#pragma warning disable 649 // We don't care about unused fields here, because they are mapped with the input file.

namespace Hubbup.MikLabelModel
{
    public class GitHubIssuePrediction
    {
        [ColumnName("PredictedLabel")]
        public string Area;

        public float[] Score;
    }
}
