namespace Hubbup.IssueMover.Dto
{
    public class MilestoneCreateResult : IErrorResult
    {
        public string MilestoneCreated { get; set; }
        public string ErrorMessage { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionStackTrace { get; set; }
    }
}
