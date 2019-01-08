using Microsoft.ML.StaticPipe;

#pragma warning disable 649 // We don't care about unsused fields here, because they are mapped with the input file.

namespace Hubbup.MikLabelModel
{
    internal class GitHubIssueTransformed
    {
        public string ID;
        public string Area;
        public string Title;
        public string Description;
        public Scalar<float> Score { get; set; }
    }
}
