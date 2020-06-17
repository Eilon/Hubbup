using Hubbup.Web.Diagnostics.Metrics;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MetricsServiceCollectionExtensions
    {
        public static IMetricsBuilder AddMetrics(this IServiceCollection services)
        {
            services.AddSingleton<IMetricsService, MetricsService>();
            return new MetricsBuilder(services);
        }

        public static IMetricsBuilder AddMetrics(this IServiceCollection services, Action<MetricsOptions> configureAction)
        {
            services.Configure(configureAction);
            return services.AddMetrics();
        }
    }
}
