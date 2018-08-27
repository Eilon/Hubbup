using System;
using System.Collections.Generic;

namespace Hubbup.IssueMover.Dto
{
    public class CommentMoveResult : IErrorResult
    {
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }
}
