using System;

namespace Hubbup.IssueMover.Dto
{
    public interface IErrorResult
    {
        string ErrorMessage { get; set; }
        Exception Exception { get; set; }
    }
}
