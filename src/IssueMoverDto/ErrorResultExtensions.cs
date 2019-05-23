using System;

namespace Hubbup.IssueMover.Dto
{
    public static class ErrorResultExtensions
    {
        public static bool HasErrors(this IErrorResult errorResult)
        {
            if (errorResult == null)
            {
                throw new ArgumentNullException(nameof(errorResult));
            }
            return errorResult.ErrorMessage != null || errorResult.ExceptionMessage != null;
        }
    }
}
