using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hubbup.Web.Diagnostics
{
    public class MetricsService : IMetricsService
    {
        private readonly object _padlock = new object();
        private readonly IReadOnlyList<IMetricsSink> _sinks;
        private readonly Timer _timer;

        private List<Metric> _metricBatch = new List<Metric>();
        private readonly ILogger<MetricsService> _logger;

        public MetricsService(IEnumerable<IMetricsSink> sinks, IOptions<MetricsOptions> options, ILogger<MetricsService> logger)
        {
            _sinks = sinks.ToList();
            _logger = logger;

            _timer = new Timer(state => ((MetricsService)state).FlushBatch(), this, options.Value.FlushRate, options.Value.FlushRate);
        }

        public void RecordMetric(Metric metric)
        {
            lock (_padlock)
            {
                _metricBatch.Add(metric);
            }
        }

        private void FlushBatch()
        {
            // Grab the batch and reset it ASAP, since it's under the lock
            List<Metric> batch;
            lock (_padlock)
            {
                batch = _metricBatch;
                _metricBatch = new List<Metric>();
            }

            // Send it to the sinks
            if (batch.Count > 0 && _sinks.Count > 0)
            {
                foreach (var sink in _sinks)
                {
                    _logger.LogTrace("Flushing batch of {Count} metrics to {SinkType}", batch.Count, sink.GetType());
                    sink.ReceiveMetrics(batch);
                }
            }
        }
    }
}
