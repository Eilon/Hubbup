using System;
using Hubbup.IssueMover.Dto;

namespace Hubbup.IssueMoverClient
{
    public class ErrorResult : IErrorResult
    {
        public string ErrorMessage { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionStackTrace { get; set; }
    }
}
