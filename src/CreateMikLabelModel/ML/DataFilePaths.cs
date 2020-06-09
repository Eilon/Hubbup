using System.IO;

namespace CreateMikLabelModel.ML
{
    public readonly struct DataFilePaths
    {
        private readonly string _prefix;
        private readonly string _customPrefix;
        public DataFilePaths(string folder, string customPrefix, bool forPrs)
        {
            Folder = folder;
            _customPrefix = customPrefix;
            InputPath = Path.Combine(Folder, _customPrefix + "issueAndPrData.tsv");
            _prefix = forPrs? "only-prs" : "only-issues";

            TrainPath = Path.Combine(Folder, _customPrefix + _prefix + "-part1.tsv");
            ValidatePath = Path.Combine(Folder, _customPrefix + _prefix + "-part2.tsv");
            TestPath = Path.Combine(Folder, _customPrefix + _prefix + "-part3.tsv");
            ModelPath = Path.Combine(Folder, _customPrefix + _prefix + "-model.zip");
            FittedModelPath = Path.Combine(Folder, _customPrefix + _prefix + "-fitted-model.zip");
            FinalModelPath = Path.Combine(Folder, _customPrefix + _prefix + "-final-model.zip");
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
