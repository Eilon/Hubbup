using System;
using System.Collections.Generic;

namespace Hubbup.IssueMover.Dto
{
    public class IssueMoveResult
    {
        public int IssueNumber { get; set; }
        public string HtmlUrl { get; set; }
    }

    public class IssueMoveRequest
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public string Milestone { get; set; }
        public string[] Assignees { get; set; }
        public string[] Labels { get; set; }
    }
}
