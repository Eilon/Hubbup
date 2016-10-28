using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hubbup.Web.Models
{
    public class RemoteJsonRepoSetProvider : JsonRepoSetProvider
    {
        public RemoteJsonRepoSetProvider(
            IOptions<RemoteJsonRepoSetProviderOptions> remoteJsonRepoSetProviderOptions,
            IHostingEnvironment hostingEnvironment,
            IApplicationLifetime applicationLifetime,
            ILogger<RemoteJsonRepoSetProvider> logger)
            : base(hostingEnvironment, applicationLifetime, logger)
        {
            RemoteJsonFileUrl = remoteJsonRepoSetProviderOptions.Value.JsonFileUrl;
        }

        public string RemoteJsonFileUrl { get; }

        protected override async Task<TextReader> GetJsonStream()
        {
            var http = new HttpClient();
            var json = await http.GetStringAsync(RemoteJsonFileUrl);
            return new StringReader(json);
        }
    }
}
