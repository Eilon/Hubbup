using Microsoft.ML;
using Microsoft.ML.AutoML;
using System.IO;

namespace CreateMikLabelModel.ML
{
    public static class MulticlassExperimentSettingsHelper
    {
        public static (ColumnInferenceResults columnInference, MulticlassExperimentSettings experimentSettings) SetupExperiment(
            MLContext mlContext, ExperimentModifier st, DataFilePaths paths, bool forPrs)
        {
            var columnInference = InferColumns(mlContext, paths.TrainPath, st.LabelColumnName);
            var columnInformation = columnInference.ColumnInformation;
            st.ColumnSetup(columnInformation, forPrs);

            var experimentSettings = new MulticlassExperimentSettings();
            st.TrainerSetup(experimentSettings.Trainers);
            experimentSettings.MaxExperimentTimeInSeconds = st.ExperimentTime;

            var cts = new System.Threading.CancellationTokenSource();
            experimentSettings.CancellationToken = cts.Token;

            // Set the cache directory to null.
            // This will cause all models produced by AutoML to be kept in memory 
            // instead of written to disk after each run, as AutoML is training.
            // (Please note: for an experiment on a large dataset, opting to keep all 
            // models trained by AutoML in memory could cause your system to run out 
            // of memory.)
            experimentSettings.CacheDirectory = new DirectoryInfo(Path.GetTempPath());
            experimentSettings.OptimizingMetric = MulticlassClassificationMetric.MicroAccuracy;
            return (columnInference, experimentSettings);
        }

        /// <summary>
        /// Infer columns in the dataset with AutoML.
        /// </summary>
        private static ColumnInferenceResults InferColumns(MLContext mlContext, string dataPath, string labelColumnName)
        {
            ConsoleHelper.ConsoleWriteHeader("=============== Inferring columns in dataset ===============");
            ColumnInferenceResults columnInference = mlContext.Auto().InferColumns(dataPath, labelColumnName, groupColumns: false);
            return columnInference;
        }
    }

}