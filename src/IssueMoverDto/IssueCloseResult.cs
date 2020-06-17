namespace Hubbup.IssueMover.Dto
{
    public class IssueCloseResult : IErrorResult
    {
        public string ErrorMessage { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionStackTrace { get; set; }
    }
}
