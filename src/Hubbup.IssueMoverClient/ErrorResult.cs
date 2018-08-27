using System;
using Hubbup.IssueMover.Dto;

namespace Hubbup.IssueMoverClient
{
    public class ErrorResult : IErrorResult
    {
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }
}
