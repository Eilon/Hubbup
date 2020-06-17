namespace Hubbup.IssueMover.Dto
{
    public class CommentMoveResult : IErrorResult
    {
        public string ErrorMessage { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionStackTrace { get; set; }
    }
}
