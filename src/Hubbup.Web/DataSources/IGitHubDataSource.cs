using System.Collections.Generic;
using System.Threading.Tasks;
using Hubbup.Web.Models;

namespace Hubbup.Web.DataSources
{
    public interface IGitHubDataSource
    {
        Task<IReadOnlyList<IssueData>> SearchIssuesAsync(string query, string accessToken);
    }
}
