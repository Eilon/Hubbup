using System;
using Hubbup.Web.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MetricsServiceCollectionExtensions
    {
        public static void AddMetrics(this IServiceCollection services)
        {
            services.AddSingleton<IMetricsService, MetricsService>();
        }

        public static void AddMetrics(this IServiceCollection services, Action<IMetricsBuilder> builder)
        {
            services.AddMetrics();
            builder(new MetricsBuilder(services));
        }

        public static void AddMetrics(this IServiceCollection services, Action<MetricsOptions> configureAction)
        {
            services.AddMetrics();
            services.Configure(configureAction);
        }

        public static void AddMetrics(this IServiceCollection services, Action<MetricsOptions> configureAction, Action<IMetricsBuilder> builder)
        {
            services.AddMetrics();
            services.Configure(configureAction);
            builder(new MetricsBuilder(services));
        }
    }
}
