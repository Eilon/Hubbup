using System;
using System.Diagnostics;
using System.Linq;
using Hubbup.MikLabelModel;
using Microsoft.ML;

namespace CreateMikLabelModel
{
    public static class MLHelper
    {
        public static void BuildAndTrainModel(string inputDataSetPath, string outputModelPath, MyTrainerStrategy selectedStrategy)
        {
            var stopWatch = Stopwatch.StartNew();

            Console.WriteLine($"Reading input TSV {inputDataSetPath}...");

            // Create MLContext to be shared across the model creation workflow objects 
            // Set a random seed for repeatable/deterministic results across multiple trainings.
            var mlContext = new MLContext(seed: 0);

            // STEP 1: Common data loading configuration
            var trainingDataView = mlContext.Data.LoadFromTextFile<GitHubIssue>(inputDataSetPath, hasHeader: true, separatorChar: '\t', allowSparse: false);

            // STEP 2: Common data process configuration with pipeline data transformations
            var dataProcessPipeline = mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "Label", inputColumnName: nameof(GitHubIssue.Area))
                            .Append(mlContext.Transforms.Text.FeaturizeText(outputColumnName: "TitleFeaturized", inputColumnName: nameof(GitHubIssue.Title)))
                            .Append(mlContext.Transforms.Text.FeaturizeText(outputColumnName: "DescriptionFeaturized", inputColumnName: nameof(GitHubIssue.Description)))
                            .Append(mlContext.Transforms.Concatenate(outputColumnName: "Features", "TitleFeaturized", "DescriptionFeaturized"))
                            .AppendCacheCheckpoint(mlContext);

            // STEP 3: Create the selected training algorithm/trainer
            IEstimator<ITransformer> trainer = null;
            switch (selectedStrategy)
            {
                case MyTrainerStrategy.SdcaMultiClassTrainer:
                    trainer = mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features");
                    break;
                case MyTrainerStrategy.OVAAveragedPerceptronTrainer:
                    {
                        // Create a binary classification trainer.
                        var averagedPerceptronBinaryTrainer = mlContext.BinaryClassification.Trainers.AveragedPerceptron("Label", "Features", numberOfIterations: 10);
                        // Compose an OVA (One-Versus-All) trainer with the BinaryTrainer.
                        // In this strategy, a binary classification algorithm is used to train one classifier for each class, "
                        // which distinguishes that class from all other classes. Prediction is then performed by running these binary classifiers, "
                        // and choosing the prediction with the highest confidence score.
                        trainer = mlContext.MulticlassClassification.Trainers.OneVersusAll(averagedPerceptronBinaryTrainer);

                        break;
                    }
                default:
                    break;
            }

            //Set the trainer/algorithm and map label to value (original readable state)
            var trainingPipeline = dataProcessPipeline.Append(trainer)
                    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // STEP 5: Train the model fitting to the DataSet
            Console.WriteLine("Training the model...");
            var trainedModel = trainingPipeline.Fit(trainingDataView);

            // STEP 6: Save/persist the trained model to a .ZIP file
            Console.WriteLine($"Saving the model to {outputModelPath}...");
            mlContext.Model.Save(trainedModel, trainingDataView.Schema, outputModelPath);

            stopWatch.Stop();
            Console.WriteLine($"Done creating model in {stopWatch.ElapsedMilliseconds}ms");
        }
    }
}
