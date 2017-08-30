using Hubbup.Web.Diagnostics;

namespace Microsoft.AspNetCore.Builder
{
    public static class RequestMetricsMiddlewareAppBuilderExtensions
    {
        public static void UseRequestMetrics(this IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseMiddleware<RequestMetricsMiddleware>();
        }
    }
}
