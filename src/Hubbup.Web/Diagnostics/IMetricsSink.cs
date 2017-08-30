using System.Collections.Generic;

namespace Hubbup.Web.Diagnostics
{
    public interface IMetricsSink
    {
        /// <summary>
        /// Called with a batch of metrics to be recorded in the storage unit
        /// </summary>
        /// <remarks>
        /// The metrics in this list are NOT guaranteed to be ordered by timestamp, but each metric will be tagged with the time at which it was created.
        /// </remarks>
        /// <param name="metrics">The metrics to record</param>
        void ReceiveMetrics(IReadOnlyList<Metric> metrics);
    }
}
