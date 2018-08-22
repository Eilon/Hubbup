using System;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Hubbup.Web.Diagnostics.Telemetry
{
    public class ApplicationInsightsRequestTelemetryListener : IRequestTelemetryListener
    {
        private readonly ILogger<ApplicationInsightsRequestTelemetryListener> _logger;

        public ApplicationInsightsRequestTelemetryListener(ILogger<ApplicationInsightsRequestTelemetryListener> logger)
        {
            _logger = logger;
        }

        public void AddProperty(HttpContext httpContext, string name, object value)
        {
            var telemetry = httpContext.Features.Get<RequestTelemetry>();
            if (telemetry == null)
            {
                _logger.LogError("Unable to record telemetry property, the Application Insights RequestTelemetry object has not been initialized yet");
            }
            else
            {
                telemetry.Properties.Add(name, Convert.ToString(value));
            }
        }
    }
}
