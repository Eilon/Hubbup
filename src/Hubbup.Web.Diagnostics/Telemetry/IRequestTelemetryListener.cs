using Microsoft.AspNetCore.Http;

namespace Hubbup.Web.Diagnostics.Telemetry
{
    public interface IRequestTelemetryListener
    {
        void AddProperty(HttpContext httpContext, string name, object value);
    }
}
