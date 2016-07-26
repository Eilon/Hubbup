using Microsoft.Extensions.Options;

namespace Hubbup.Web.Models
{
    public class LocalJsonRepoSetProviderOptions : IOptions<LocalJsonRepoSetProviderOptions>
    {
        public string JsonFilePath { get; set; }

        LocalJsonRepoSetProviderOptions IOptions<LocalJsonRepoSetProviderOptions>.Value
        {
            get
            {
                return this;
            }
        }
    }
}
