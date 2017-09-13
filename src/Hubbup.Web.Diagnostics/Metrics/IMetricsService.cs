namespace Hubbup.Web.Diagnostics.Metrics
{
    public interface IMetricsService
    {
        void RecordMetric(Metric metric);
    }
}
