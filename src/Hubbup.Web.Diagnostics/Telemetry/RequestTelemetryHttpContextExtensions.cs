using Hubbup.Web.Diagnostics.Telemetry;

namespace Microsoft.AspNetCore.Http
{
    public static class RequestTelemetryHttpContextExtensions
    {
        public static void AddTelemetryProperty(this HttpContext context, string name, object value)
        {
            var telemetryFeature = context.Features.Get<IRequestTelemetryFeature>();

            // Silently fail if there is no telemetry feature
            telemetryFeature?.AddProperty(name, value);
        }
    }
}
