using Microsoft.Extensions.Options;

namespace Hubbup.Web.Models
{
    public class RemoteJsonRepoSetProviderOptions : IOptions<RemoteJsonRepoSetProviderOptions>
    {
        public string JsonFileUrl { get; set; }

        RemoteJsonRepoSetProviderOptions IOptions<RemoteJsonRepoSetProviderOptions>.Value
        {
            get
            {
                return this;
            }
        }
    }
}
