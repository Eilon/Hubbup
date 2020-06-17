using Hubbup.MikLabelModel;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CreateMikLabelModel.ML
{
    public static class MulticlassExperimentHelper
    {
        public static ExperimentResult<MulticlassClassificationMetrics> RunAutoMLExperiment(
            MLContext mlContext, string labelColumnName, MulticlassExperimentSettings experimentSettings,
            MulticlassExperimentProgressHandler progressHandler, IDataView dataView)
        {
            ConsoleHelper.ConsoleWriteHeader("=============== Running AutoML experiment ===============");
            Console.WriteLine($"Running AutoML multiclass classification experiment for {experimentSettings.MaxExperimentTimeInSeconds} seconds...");
            var experimentResult = mlContext.Auto()
                .CreateMulticlassClassificationExperiment(experimentSettings)
                .Execute(dataView, labelColumnName, progressHandler: progressHandler);

            Console.WriteLine(Environment.NewLine);
            Console.WriteLine($"num models created: {experimentResult.RunDetails.Count()}");

            // Get top few runs ranked by accuracy
            var topRuns = experimentResult.RunDetails
                .Where(r => r.ValidationMetrics != null && !double.IsNaN(r.ValidationMetrics.MicroAccuracy))
                .OrderByDescending(r => r.ValidationMetrics.MicroAccuracy).Take(3);

            Console.WriteLine("Top models ranked by accuracy --");
            CreateRow($"{"",-4} {"Trainer",-35} {"MicroAccuracy",14} {"MacroAccuracy",14} {"Duration",9}", Width);
            for (var i = 0; i < topRuns.Count(); i++)
            {
                var run = topRuns.ElementAt(i);
                CreateRow($"{i,-4} {run.TrainerName,-35} {run.ValidationMetrics?.MicroAccuracy ?? double.NaN,14:F4} {run.ValidationMetrics?.MacroAccuracy ?? double.NaN,14:F4} {run.RuntimeInSeconds,9:F1}", Width);
            }
            return experimentResult;
        }

        public static ExperimentResult<MulticlassClassificationMetrics> Train(
            MLContext mlContext, string labelColumnName, MulticlassExperimentSettings experimentSettings,
            MulticlassExperimentProgressHandler progressHandler, DataFilePaths paths, TextLoader textLoader)
        {
            var trainData = textLoader.Load(paths.TrainPath);
            var validateData = textLoader.Load(paths.ValidatePath);
            var experimentResult = RunAutoMLExperiment(mlContext, labelColumnName, experimentSettings, progressHandler, trainData);
            EvaluateTrainedModelAndPrintMetrics(mlContext, experimentResult.BestRun.Model, experimentResult.BestRun.TrainerName, validateData);
            SaveModel(mlContext, experimentResult.BestRun.Model, paths.ModelPath, trainData);
            return experimentResult;
        }

        public static ITransformer Retrain(ExperimentResult<MulticlassClassificationMetrics> experimentResult,
            string trainerName, MultiFileSource multiFileSource, string dataPath, string modelPath, TextLoader textLoader, MLContext mlContext)
        {
            var dataView = textLoader.Load(dataPath);

            ConsoleHelper.ConsoleWriteHeader("=============== Re-fitting best pipeline ===============");
            var combinedDataView = textLoader.Load(multiFileSource);

            var bestRun = experimentResult.BestRun;
            var refitModel = bestRun.Estimator.Fit(combinedDataView);

            EvaluateTrainedModelAndPrintMetrics(mlContext, refitModel, trainerName, dataView);
            SaveModel(mlContext, refitModel, modelPath, dataView);
            return refitModel;
        }

        public static ITransformer Retrain(MLContext mlContext, ExperimentResult<MulticlassClassificationMetrics> experimentResult,
            ColumnInferenceResults columnInference, DataFilePaths paths, bool fixedBug = false)
        {
            ConsoleHelper.ConsoleWriteHeader("=============== Re-fitting best pipeline ===============");
            var textLoader = mlContext.Data.CreateTextLoader(columnInference.TextLoaderOptions);
            var combinedDataView = textLoader.Load(new MultiFileSource(paths.TrainPath, paths.ValidatePath, paths.TestPath));
            var bestRun = experimentResult.BestRun;
            if (fixedBug)
            {
                // TODO: retry: below gave error but I thought it would work:
                //refitModel = MulticlassExperiment.Retrain(experimentResult, 
                //    "final model", 
                //    new MultiFileSource(paths.TrainPath, paths.ValidatePath, paths.FittedPath), 
                //    paths.TestPath, 
                //    paths.FinalPath, textLoader, mlContext);
                // but if failed before fixing this maybe the problem was in *EvaluateTrainedModelAndPrintMetrics*

            }
            var refitModel = bestRun.Estimator.Fit(combinedDataView);

            EvaluateTrainedModelAndPrintMetrics(mlContext, refitModel, "production model", textLoader.Load(paths.TestPath));
            // Save the re-fit model to a.ZIP file
            SaveModel(mlContext, refitModel, paths.FinalModelPath, textLoader.Load(paths.TestPath));

            Console.WriteLine("The model is saved to {0}", paths.FinalModelPath);
            return refitModel;
        }

        private const int Width = 114;

        private static void CreateRow(string message, int width)
        {
            Console.WriteLine("|" + message.PadRight(width - 2) + "|");
        }

        /// <summary>
        /// Evaluate the model and print metrics.
        /// </summary>
        private static void EvaluateTrainedModelAndPrintMetrics(MLContext mlContext, ITransformer model, string trainerName, IDataView dataView)
        {
            Console.WriteLine("===== Evaluating model's accuracy with test data =====");
            var predictions = model.Transform(dataView);
            var metrics = mlContext.MulticlassClassification.Evaluate(predictions, labelColumnName: "Area", scoreColumnName: "Score");

            Console.WriteLine($"************************************************************");
            Console.WriteLine($"*    Metrics for {trainerName} multi-class classification model   ");
            Console.WriteLine($"*-----------------------------------------------------------");
            Console.WriteLine($"    MacroAccuracy = {metrics.MacroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
            Console.WriteLine($"    MicroAccuracy = {metrics.MicroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
            Console.WriteLine($"    LogLoss = {metrics.LogLoss:0.####}, the closer to 0, the better");
            Console.WriteLine($"    LogLoss for class 1 = {metrics.PerClassLogLoss[0]:0.####}, the closer to 0, the better");
            Console.WriteLine($"    LogLoss for class 2 = {metrics.PerClassLogLoss[1]:0.####}, the closer to 0, the better");
            Console.WriteLine($"    LogLoss for class 3 = {metrics.PerClassLogLoss[2]:0.####}, the closer to 0, the better");
            Console.WriteLine($"************************************************************");
        }

        private static void SaveModel(MLContext mlContext, ITransformer model, string modelPath, IDataView dataview)
        {
            // Save the re-fit model to a.ZIP file
            ConsoleHelper.ConsoleWriteHeader("=============== Saving the model ===============");
            mlContext.Model.Save(model, dataview.Schema, modelPath);
            Trace.WriteLine("The model is saved to {0}", modelPath);
            Console.WriteLine("The model is saved to {0}", modelPath);
        }

        public static void TestPrediction(MLContext mlContext, DataFilePaths files, bool forPrs, double threshold = 0.4)
        {
            var trainedModel = mlContext.Model.Load(files.FittedModelPath, out _);
            IEnumerable<(string knownLabel, GitHubIssuePrediction predictedResult)> predictions = null;
            if (forPrs)
            {
                var testData = GetPullRequests(mlContext, files.TestPath);
                Console.WriteLine($"count: {testData.Length}");
                var prEngine = mlContext.Model.CreatePredictionEngine<GitHubPullRequest, GitHubIssuePrediction>(trainedModel);
                predictions = testData
                   .Select(x => (knownLabel: x.Area, predictedResult: prEngine.Predict(x)));
            }
            else
            {
                var testData = GetIssues(mlContext, files.TestPath);
                Console.WriteLine($"count: {testData.Length}");
                var issueEngine = mlContext.Model.CreatePredictionEngine<GitHubIssue, GitHubIssuePrediction>(trainedModel);
                predictions = testData
                   .Select(x => (knownLabel: x.Area, predictedResult: issueEngine.Predict(x)));
            }

            var analysis =
                predictions.Select(x =>
                (
                    knownLabel: x.knownLabel,
                    predictedArea: x.predictedResult.Area,
                    confidentInPrediction: x.predictedResult.Score.Max() >= threshold
                ));

            var countSuccess = analysis.Where(x =>
                    (x.confidentInPrediction && x.predictedArea.Equals(x.knownLabel, StringComparison.Ordinal)) ||
                    (!x.confidentInPrediction && !x.predictedArea.Equals(x.knownLabel, StringComparison.Ordinal))).Count();

            var missedOpportunity = analysis
                .Where(x => !x.confidentInPrediction && x.knownLabel.Equals(x.predictedArea, StringComparison.Ordinal)).Count();

            var mistakes = analysis
                .Where(x => x.confidentInPrediction && !x.knownLabel.Equals(x.predictedArea, StringComparison.Ordinal))
                .Select(x => $"Predicted: {x.predictedArea}, Actual:{x.knownLabel}")
                .GroupBy(x => x)
                .Select(x => new
                {
                    Count = x.Count(),
                    Name = x.Key
                })
                .OrderByDescending(x => x.Count);

            Console.WriteLine($"countSuccess: {countSuccess}, missed: {missedOpportunity}");
            foreach (var mismatch in mistakes.AsEnumerable())
            {
                Console.WriteLine($"{mismatch.Name}, NumFound:{mismatch.Count}");
            }
        }

        public static GitHubIssue[] GetIssues(MLContext mlContext, string dataFilePath)
        {
            var dataView = mlContext.Data.LoadFromTextFile<GitHubIssue>(
                                            path: dataFilePath,
                                            hasHeader: true,
                                            separatorChar: '\t',
                                            allowQuoting: true,
                                            allowSparse: false);

            return mlContext.Data.CreateEnumerable<GitHubIssue>(dataView, false).ToArray();
        }

        public static GitHubPullRequest[] GetPullRequests(MLContext mlContext, string dataFilePath)
        {
            var dataView = mlContext.Data.LoadFromTextFile<GitHubPullRequest>(
                                            path: dataFilePath,
                                            hasHeader: true,
                                            separatorChar: '\t',
                                            allowQuoting: true,
                                            allowSparse: false);

            return mlContext.Data.CreateEnumerable<GitHubPullRequest>(dataView, false).ToArray();
        }
    }
}
