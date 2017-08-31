using Microsoft.Extensions.DependencyInjection;

namespace Hubbup.Web.Diagnostics.Metrics
{
    // Marker interface to hang .Add[MetricSinkName] extensions off of.
    public interface IMetricsBuilder
    {
        IServiceCollection Services { get; }
    }
}
