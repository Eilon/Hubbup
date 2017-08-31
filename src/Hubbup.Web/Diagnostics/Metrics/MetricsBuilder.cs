using Hubbup.Web.Diagnostics.Metrics;

namespace Microsoft.Extensions.DependencyInjection
{
    public class MetricsBuilder : IMetricsBuilder
    {
        public IServiceCollection Services { get; }

        public MetricsBuilder(IServiceCollection services)
        {
            Services = services;
        }
    }
}
