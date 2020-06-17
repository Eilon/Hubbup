using System.IO;

namespace CreateMikLabelModel.ML
{
    public readonly struct DataFilePaths
    {
        public DataFilePaths(string folder, string customPrefix, bool forPrs)
        {
            Folder = folder;
            InputPath = Path.Combine(Folder, customPrefix + "issueAndPrData.tsv");
            var prefix = forPrs ? "only-prs" : "only-issues";

            TrainPath = Path.Combine(Folder, customPrefix + prefix + "-part1.tsv");
            ValidatePath = Path.Combine(Folder, customPrefix + prefix + "-part2.tsv");
            TestPath = Path.Combine(Folder, customPrefix + prefix + "-part3.tsv");
            ModelPath = Path.Combine(Folder, customPrefix + prefix + "-model.zip");
            FittedModelPath = Path.Combine(Folder, customPrefix + prefix + "-fitted-model.zip");
            FinalModelPath = Path.Combine(Folder, customPrefix + prefix + "-final-model.zip");
        }

        public string Folder { get; }
        public readonly string TrainPath;
        public readonly string ValidatePath;
        public readonly string TestPath;
        public readonly string ModelPath;
        public readonly string FittedModelPath;
        public readonly string FinalModelPath;
        public readonly string InputPath;
    }
}
