using System;

namespace Hubbup.IssueMover.Dto
{
    public interface IErrorResult
    {
        string ErrorMessage { get; set; }
        string ExceptionMessage { get; set; }
        string ExceptionStackTrace { get; set; }
    }
}
