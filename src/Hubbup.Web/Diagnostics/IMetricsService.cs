namespace Hubbup.Web.Diagnostics
{
    public interface IMetricsService
    {
        void RecordMetric(Metric metric);
    }
}
