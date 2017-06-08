using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hubbup.Web.DataSources
{
    public class RemoteJsonDataSource : JsonDataSource
    {
        public RemoteJsonDataSource(
            IOptions<RemoteJsonDataSourceOptions> remoteJsonRepoSetProviderOptions,
            IHostingEnvironment hostingEnvironment,
            IApplicationLifetime applicationLifetime,
            ILogger<RemoteJsonDataSource> logger)
            : base(hostingEnvironment, applicationLifetime, logger)
        {
            RemoteUrlBase = remoteJsonRepoSetProviderOptions.Value.BaseUrl;
        }

        public string RemoteUrlBase { get; }

        protected override async Task<ReadFileResult> ReadJsonStream(string fileName, string etag)
        {
            var url = RemoteUrlBase;
            if (!url.EndsWith('/'))
            {
                url += "/";
            }
            url += fileName;

            var http = new HttpClient();
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(etag))
            {
                req.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
            }

            var resp = await http.SendAsync(req);
            if (resp.StatusCode == HttpStatusCode.NotModified)
            {
                return new ReadFileResult();
            }
            else
            {
                return new ReadFileResult(content: new StreamReader(await resp.Content.ReadAsStreamAsync()), etag: resp.Headers.ETag?.Tag);
            }
        }
    }
}
