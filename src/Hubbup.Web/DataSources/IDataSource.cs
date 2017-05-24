using System.Threading;
using System.Threading.Tasks;
using Hubbup.Web.Models;

namespace Hubbup.Web.DataSources
{
    public interface IDataSource
    {
        PersonSet GetPersonSet(string personSetName);
        RepoDataSet GetRepoDataSet();

        Task ReloadAsync(CancellationToken cancellationToken);
    }
}
