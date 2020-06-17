using System;
using System.Diagnostics;

namespace Hubbup.Web.Diagnostics.Metrics
{
    public struct TimeMetricDisposable : IDisposable
    {
        private readonly IMetricsService _metricsService;
        private readonly string _measurement;
        private readonly Stopwatch _stopwatch;

        public TimeMetricDisposable(IMetricsService metricsService, string measurement)
        {
            _metricsService = metricsService;
            _measurement = measurement;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _metricsService.RecordMetric(new Metric(_measurement, _stopwatch.Elapsed));
        }
    }
}
