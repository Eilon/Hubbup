using System;

namespace Hubbup.IssueMover.Dto
{
    public class RepoMoveData : IErrorResult
    {
        public string Owner { get; set; }
        public string Repo { get; set; }
        public int OpenIssueCount { get; set; }
        public Exception Exception { get; set; }
        public string ErrorMessage { get; set; }
    }
}
