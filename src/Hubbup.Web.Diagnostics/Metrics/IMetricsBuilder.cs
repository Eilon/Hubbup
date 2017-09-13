using Microsoft.Extensions.DependencyInjection;

namespace Hubbup.Web.Diagnostics.Metrics
{
    public interface IMetricsBuilder
    {
        IServiceCollection Services { get; }
    }
}
