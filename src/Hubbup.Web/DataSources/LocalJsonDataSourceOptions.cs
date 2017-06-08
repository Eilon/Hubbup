using Microsoft.Extensions.Options;

namespace Hubbup.Web.DataSources
{
    public class LocalJsonDataSourceOptions : IOptions<LocalJsonDataSourceOptions>
    {
        public string Path { get; set; }

        LocalJsonDataSourceOptions IOptions<LocalJsonDataSourceOptions>.Value
        {
            get
            {
                return this;
            }
        }
    }
}
