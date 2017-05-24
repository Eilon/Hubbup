using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hubbup.Web.DataSources
{
    public class LocalJsonDataSource : JsonDataSource
    {
        public LocalJsonDataSource(
            IOptions<LocalJsonDataSourceOptions> localJsonRepoSetProviderOptions,
            IHostingEnvironment hostingEnvironment,
            IApplicationLifetime applicationLifetime,
            ILogger<LocalJsonDataSource> logger,
            TelemetryClient telemetryClient)
            : base(hostingEnvironment, applicationLifetime, logger, telemetryClient)
        {
            BasePath = Path.Combine(hostingEnvironment.ContentRootPath, localJsonRepoSetProviderOptions.Value.Path);
        }

        public string BasePath { get; }

        protected override Task<ReadFileResult> ReadJsonStream(string fileName, string etag)
        {
            var path = Path.Combine(BasePath, fileName);

            // Etag should be a date
            if(etag != null && DateTime.TryParseExact(etag, "O", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date))
            {
                // Check if the file has changed
                if(File.GetLastWriteTimeUtc(path) <= date)
                {
                    return UnchangedReadResultTask;
                }
            }

            return Task.FromResult(new ReadFileResult(
                content: File.OpenText(path),
                etag: File.GetLastWriteTimeUtc(path).ToString("O")));
        }
    }
}
