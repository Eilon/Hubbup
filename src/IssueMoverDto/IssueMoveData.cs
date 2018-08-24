using System;
using System.Collections.Generic;

namespace Hubbup.IssueMover.Dto
{
    public class IssueMoveData
    {
        public string RepoOwner { get; set; }
        public string RepoName { get; set; }
        public IssueState State { get; set; }
        public string HtmlUrl { get; set; }
        public string Title { get; set; }
        public int Number { get; set; }
        public string Body { get; set; }
        public string Author { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public string[] Assignees { get; set; }
        public List<LabelData> Labels { get; set; }
        public string Milestone { get; set; }
        public List<CommentData> Comments { get; set; }
    }

    public class LabelData
    {
        public string Text { get; set; }
        public string Color { get; set; }
    }

    public class CommentData
    {
        public string Author { get; set; }
        public string Text { get; set; }
        public DateTimeOffset Date { get; set; }

    }
    public class RepoMoveData
    {
        public string Owner { get; set; }
        public string Repo { get; set; }
        public int OpenIssueCount { get; set; }
    }
}
