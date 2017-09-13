using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Hubbup.Web.Diagnostics.Telemetry
{
    public class RequestTelemetryMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RequestTelemetryMiddleware> _logger;
        private readonly IEnumerable<IRequestTelemetryListener> _listeners;

        public RequestTelemetryMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, IEnumerable<IRequestTelemetryListener> listeners)
        {
            _next = next;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<RequestTelemetryMiddleware>();
            _listeners = listeners;
        }

        public Task Invoke(HttpContext context)
        {
            context.Features.Set<IRequestTelemetryFeature>(new RequestTelemetryFeature(_listeners, context, _loggerFactory.CreateLogger<RequestTelemetryFeature>()));
            return _next(context);
        }
    }
}
