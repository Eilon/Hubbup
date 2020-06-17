namespace Hubbup.IssueMover.Dto
{
    public class IssueLockResult : IErrorResult
    {
        public string ErrorMessage { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionStackTrace { get; set; }
    }
}
