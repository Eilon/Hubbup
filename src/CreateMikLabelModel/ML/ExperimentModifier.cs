using Microsoft.ML.AutoML;
using System;
using System.Collections.Generic;

namespace CreateMikLabelModel.ML
{
    public struct ExperimentModifier
    {
        public ExperimentModifier(DataFilePaths paths, bool forPrs)
        {
            // set all to defaults:
            ColumnSetup = (columnInformation, forPrs) =>
            {
                // Customize column information returned by InferColumns API
                columnInformation.CategoricalColumnNames.Clear();
                columnInformation.NumericColumnNames.Clear();
                columnInformation.IgnoredColumnNames.Clear();
                columnInformation.TextColumnNames.Clear();

                // NOTE: depending on how the data changes over time this might need to get updated too.
                columnInformation.TextColumnNames.Add("Title");
                columnInformation.TextColumnNames.Add("Description");
                columnInformation.IgnoredColumnNames.Add("IssueAuthor");
                columnInformation.IgnoredColumnNames.Add("IsPR");
                columnInformation.IgnoredColumnNames.Add("NumMentions");
                columnInformation.IgnoredColumnNames.Add("UserMentions");

                if (forPrs)
                {
                    columnInformation.NumericColumnNames.Add("FileCount");
                    columnInformation.CategoricalColumnNames.Add("Files");
                    columnInformation.CategoricalColumnNames.Add("FolderNames");
                    columnInformation.CategoricalColumnNames.Add("Folders");
                    columnInformation.IgnoredColumnNames.Add("FileExtensions");
                    columnInformation.IgnoredColumnNames.Add("Filenames");
                }
            };

            TrainerSetup = (trainers) =>
            {
                trainers.Clear();
                trainers.Add(MulticlassClassificationTrainer.SdcaMaximumEntropy);
            };

            ExperimentTime = 60;
            LabelColumnName = "Area";
            ForPrs = forPrs;
            Paths = paths;
        }

        public ExperimentModifier(
            bool forPrs,
            uint experimentTime,
            string labelColumnName,
            DataFilePaths paths,
            Action<ColumnInformation, bool> columnSetup,
            Action<ICollection<MulticlassClassificationTrainer>> trainerSetup)
        {
            ForPrs = forPrs;
            ExperimentTime = experimentTime;
            LabelColumnName = labelColumnName;
            Paths = paths;
            ColumnSetup = columnSetup;
            TrainerSetup = trainerSetup;
        }

        public readonly uint ExperimentTime;
        public readonly string LabelColumnName;
        public readonly Action<ColumnInformation, bool> ColumnSetup;
        public readonly Action<ICollection<MulticlassClassificationTrainer>> TrainerSetup;
        public readonly bool ForPrs;
        public readonly DataFilePaths Paths;
    }
}
