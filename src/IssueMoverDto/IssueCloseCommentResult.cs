using System;
using System.Collections.Generic;

namespace Hubbup.IssueMover.Dto
{
    public class IssueCloseCommentResult
    {
    }

    public class IssueCloseCommentRequest
    {
        public int IssueNumber { get; set; }
        public string Comment { get; set; }
    }
}
