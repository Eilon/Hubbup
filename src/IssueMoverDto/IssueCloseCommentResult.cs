using System;
using System.Collections.Generic;

namespace Hubbup.IssueMover.Dto
{
    public class IssueCloseCommentResult : IErrorResult
    {
        public string ErrorMessage { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionStackTrace { get; set; }
    }
}
