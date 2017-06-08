using System.Collections.Generic;

namespace Hubbup.Web.DataSources
{
    public class GraphQlQueryRequest
    {
        public string Query { get; }
        public IDictionary<string, object> Variables { get; } = new Dictionary<string, object>();

        public GraphQlQueryRequest(string query)
        {
            Query = query;
        }
    }
}
