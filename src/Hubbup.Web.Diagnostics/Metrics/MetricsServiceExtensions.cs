using System;

namespace Hubbup.Web.Diagnostics.Metrics
{
    public static class MetricsServiceExtensions
    {
        public static void Record(this IMetricsService metricsService, string measurement, double value) => metricsService.RecordMetric(new Metric(measurement, value));
        public static void Record(this IMetricsService metricsService, string measurement, TimeSpan value) => metricsService.RecordMetric(new Metric(measurement, value));
        public static void Increment(this IMetricsService metricsService, string measurement, int amount = 1) => metricsService.RecordMetric(new Metric(measurement, amount));

        // Using the real type instead of IDisposable in order to avoid having to box the struct.
        public static TimeMetricDisposable Time(this IMetricsService metricsService, string measurement)
        {
            return new TimeMetricDisposable(metricsService, measurement);
        }
    }
}
