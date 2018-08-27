using Hubbup.IssueMover.Dto;

namespace Hubbup.IssueMoverClient
{
    public class ErrorLogEntry
    {
        public string Description { get; set; }
        public IErrorResult ErrorResult { get; set; }
    }
}
