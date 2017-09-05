using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hubbup.Web.Diagnostics.Metrics
{
    public class ApplicationInsightsMetricsSink : IMetricsSink
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger<ApplicationInsightsMetricsSink> _logger;

        public ApplicationInsightsMetricsSink(IServiceProvider services, ILogger<ApplicationInsightsMetricsSink> logger)
        {
            // This is so we don't fail when App Insights isn't autoregistered by HostingStartup. In the future, this should live inside the HostingStartup so it doesn't need to do this
            _telemetryClient = services.GetService<TelemetryClient>();

            _logger = logger;
        }

        public void ReceiveMetrics(IReadOnlyList<Metric> metrics)
        {
            if(_telemetryClient == null)
            {
                _logger.LogTrace("Not recording metrics because Application Insights was not registered");
                return;
            }

            // Group metrics by measurement name
            foreach (var grouping in metrics.GroupBy(m => m.Measurement))
            {
                // Calculate the necessary aggregates
                // TODO: Consider tracking these aggregates in the MetricsService? 
                int count = 0;
                double sum = 0;
                double sumOfSquares = 0;
                double? min = null;
                double? max = null;
                foreach (var item in grouping)
                {
                    count += 1;
                    sum += item.Value;
                    sumOfSquares += item.Value * item.Value;
                    min = (min == null || min.Value > item.Value) ? item.Value : min;
                    max = (max == null || max.Value < item.Value) ? item.Value : max;
                }

                var mean = sum / count;
                var meanSquared = mean * mean;
                var variance = (sumOfSquares / count) - meanSquared;
                var stdDev = Math.Sqrt(variance);

                _logger.LogDebug("Recording Metric Telemetry: {Name} (Count={Count}, Sum={Sum}, Min={Min}, Max={Max}, StdDev={StdDev})", grouping.Key, count, sum, min.Value, max.Value, stdDev);
                var metricTelemetry = new MetricTelemetry(grouping.Key, count, sum, min.Value, max.Value, stdDev);
                _telemetryClient.TrackMetric(metricTelemetry);
            }
        }
    }
}
