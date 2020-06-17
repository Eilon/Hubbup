using Hubbup.Web.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hubbup.Web.DataSources
{
    public interface IGitHubDataSource
    {
        Task<SearchResults<IReadOnlyList<IssueData>>> SearchIssuesAsync(string query, string accessToken);
    }
}
