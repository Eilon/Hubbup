using Microsoft.Extensions.Options;

namespace Hubbup.Web.DataSources
{
    public class RemoteJsonDataSourceOptions : IOptions<RemoteJsonDataSourceOptions>
    {
        public string BaseUrl { get; set; }

        RemoteJsonDataSourceOptions IOptions<RemoteJsonDataSourceOptions>.Value
        {
            get
            {
                return this;
            }
        }
    }
}
