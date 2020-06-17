using Hubbup.Web.DataSources;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Hubbup.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateWebHostBuilder(args).Build();

            // Load data before we start things up.
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Loading repo set and person set data...");
            var dataSource = host.Services.GetRequiredService<IDataSource>();
            await dataSource.ReloadAsync(default);
            logger.LogDebug("Loaded repo set and person set data");

            await host.RunAsync();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}
