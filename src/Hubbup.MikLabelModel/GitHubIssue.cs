#pragma warning disable 649 // We don't care about unsused fields here, because they are mapped with the input file.

using Microsoft.ML.Data;
using System;

namespace Hubbup.MikLabelModel
{
    public class GitHubPullRequest : GitHubIssue
    {
        [LoadColumn(9)]
        public Single FileCount;

        [LoadColumn(10)]
        public string Files;

        [LoadColumn(11)]
        public string Filenames;

        [LoadColumn(12)]
        public string FileExtensions;

        [LoadColumn(13)]
        public string FolderNames;

        [LoadColumn(14)]
        public string Folders;
    }

    public class GitHubIssue
    {
        [LoadColumn(0)]
        public string CombinedID;

        [LoadColumn(1)]
        public Single ID;

        [LoadColumn(2)]
        public string Area;

        [LoadColumn(3)]
        public string Title;

        [LoadColumn(4)]
        public string Description;

        [LoadColumn(5)]
        public string Author;

        [LoadColumn(6)]
        public float IsPR;

        [LoadColumn(7)]
        public string UserMentions;

        [LoadColumn(8)]
        public Single NumMentions;
    }
}
