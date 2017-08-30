using Hubbup.Web.Diagnostics;

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
