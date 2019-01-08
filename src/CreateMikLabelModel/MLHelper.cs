using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Hubbup.MikLabelModel;
using Microsoft.ML;
using Microsoft.ML.Core.Data;
using Microsoft.ML.Runtime.Data;

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
            var textLoader = mlContext.Data.TextReader(new TextLoader.Arguments()
            {
                Separator = "tab",
                HasHeader = true,
                Column = new[]
                {
                    new TextLoader.Column("ID", DataKind.Text, 0),
                    new TextLoader.Column("Area", DataKind.Text, 1),
                    new TextLoader.Column("Title", DataKind.Text, 2),
                    new TextLoader.Column("Description", DataKind.Text, 3),
                }
            });

            var trainingDataView = textLoader.Read(inputDataSetPath);

            // STEP 2: Common data process configuration with pipeline data transformations
            var dataProcessPipeline = mlContext.Transforms.Conversion.MapValueToKey("Area", "Label")
                            .Append(mlContext.Transforms.Text.FeaturizeText("Title", "TitleFeaturized"))
                            .Append(mlContext.Transforms.Text.FeaturizeText("Description", "DescriptionFeaturized"))
                            .Append(mlContext.Transforms.Concatenate("Features", "TitleFeaturized", "DescriptionFeaturized"))
                            //Sample Caching the DataView so estimators iterating over the data multiple times, instead of always reading from file, using the cache might get better performance
                            .AppendCacheCheckpoint(mlContext);  //In this sample, only when using OVA (Not SDCA) the cache improves the training time, since OVA works multiple times/iterations over the same data

            // STEP 3: Create the selected training algorithm/trainer
            IEstimator<ITransformer> trainer = null;
            switch (selectedStrategy)
            {
                case MyTrainerStrategy.SdcaMultiClassTrainer:
                    trainer = mlContext.MulticlassClassification.Trainers.StochasticDualCoordinateAscent(
                        DefaultColumnNames.Label,
                        DefaultColumnNames.Features);
                    break;
                case MyTrainerStrategy.OVAAveragedPerceptronTrainer:
                    {
                        // Create a binary classification trainer.
                        var averagedPerceptronBinaryTrainer =
                            mlContext.BinaryClassification.Trainers.AveragedPerceptron(
                                DefaultColumnNames.Label,
                                DefaultColumnNames.Features,
                                numIterations: 10);

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

            //// STEP 4: Cross-Validate with single dataset (since we don't have two datasets, one for training and for evaluate)
            //// in order to evaluate and get the model's accuracy metrics
            ////(CDLTLL-UNDO)
            //Console.WriteLine("=============== Cross-validating to get model's accuracy metrics ===============");

            //var crossValidationResults = mlContext.MulticlassClassification.CrossValidate(trainingDataView, trainingPipeline, numFolds: 6, labelColumn: "Label");
            //ConsoleHelper.PrintMulticlassClassificationFoldsAverageMetrics(trainer.ToString(), crossValidationResults);

            // STEP 5: Train the model fitting to the DataSet
            Console.WriteLine("Training the model...");

            var trainedModel = trainingPipeline.Fit(trainingDataView);

            // STEP 6: Save/persist the trained model to a .ZIP file
            Console.WriteLine($"Saving the model to {outputModelPath}...");
            using (var fs = File.OpenWrite(outputModelPath))
            {
                mlContext.Model.Save(trainedModel, fs);
            }

            stopWatch.Stop();
            Console.WriteLine($"Done creating model in {stopWatch.ElapsedMilliseconds}ms");
        }
    }
}
