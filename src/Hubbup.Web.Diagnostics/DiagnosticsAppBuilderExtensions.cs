using Hubbup.Web.Diagnostics.Metrics;
using Hubbup.Web.Diagnostics.Telemetry;

namespace Microsoft.AspNetCore.Builder
{
    public static class DiagnosticsAppBuilderExtensions
    {
        public static void UseRequestMetrics(this IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseMiddleware<RequestMetricsMiddleware>();
        }

        public static void UseRequestTelemetry(this IApplicationBuilder applicationBuidler)
        {
            applicationBuidler.UseMiddleware<RequestTelemetryMiddleware>();
        }

        public static void UseDiagnostics(this IApplicationBuilder applicationBuilder)
        {
            applicationBuilder.UseRequestTelemetry();
            applicationBuilder.UseRequestMetrics();
        }
    }
}
