using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Hubbup.Web.Diagnostics.Metrics
{
    public class RequestMetricsMiddleware
    {
        private static readonly string RequestDurationMeasurementName = "RequestDuration";

        private readonly RequestDelegate _next;
        private readonly ILogger<RequestMetricsMiddleware> _logger;
        private readonly IMetricsService _metrics;

        public RequestMetricsMiddleware(RequestDelegate next, ILogger<RequestMetricsMiddleware> logger, IMetricsService metrics)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));

            _logger = logger;
            _metrics = metrics;
        }

        public async Task Invoke(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                _metrics.RecordMetric(new Metric(RequestDurationMeasurementName, stopwatch.Elapsed));
            }
        }
    }
}
