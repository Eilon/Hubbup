using System;
using System.Collections.Generic;

namespace Hubbup.IssueMover.Dto
{
    public class CommentMoveResult
    {
    }

    public class CommentMoveRequest
    {
        public int IssueNumber { get; set; }
        public string Text { get; set; }
    }
}
