using System;
using System.Collections.Generic;

namespace Hubbup.IssueMover.Dto
{
    public class IssueMoveResult : IErrorResult
    {
        public int IssueNumber { get; set; }
        public string HtmlUrl { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }
}
