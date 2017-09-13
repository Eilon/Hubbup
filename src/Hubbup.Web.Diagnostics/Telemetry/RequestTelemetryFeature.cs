using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Hubbup.Web.Diagnostics.Telemetry
{
    public class RequestTelemetryFeature : IRequestTelemetryFeature
    {
        private readonly IEnumerable<IRequestTelemetryListener> _listeners;
        private readonly HttpContext _httpContext;
        private readonly ILogger<RequestTelemetryFeature> _logger;

        public RequestTelemetryFeature(IEnumerable<IRequestTelemetryListener> listeners, HttpContext httpContext, ILogger<RequestTelemetryFeature> logger)
        {
            _listeners = listeners;
            _httpContext = httpContext;
            _logger = logger;
        }

        public void AddProperty(string name, object value)
        {
            _logger.LogTrace("Recording request telemetry value: {Name} = {Value}", name, value);
            foreach (var listener in _listeners)
            {
                listener.AddProperty(_httpContext, name, value);
            }
        }
    }
}
