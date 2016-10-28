using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hubbup.Web.Models
{
    public class LocalJsonRepoSetProvider : JsonRepoSetProvider
    {
        public LocalJsonRepoSetProvider(
            IOptions<LocalJsonRepoSetProviderOptions> localJsonRepoSetProviderOptions,
            IHostingEnvironment hostingEnvironment,
            IApplicationLifetime applicationLifetime,
            ILogger<LocalJsonRepoSetProvider> logger)
            : base(hostingEnvironment, applicationLifetime, logger)
        {
            PhysicalJsonFilePath = Path.Combine(hostingEnvironment.ContentRootPath, localJsonRepoSetProviderOptions.Value.JsonFilePath);
        }

        public string PhysicalJsonFilePath { get; }

        protected override Task<TextReader> GetJsonStream()
        {
            return Task.FromResult<TextReader>(File.OpenText(PhysicalJsonFilePath));
        }
    }
}
