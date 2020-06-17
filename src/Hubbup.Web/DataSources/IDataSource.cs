using Hubbup.Web.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Hubbup.Web.DataSources
{
    public interface IDataSource
    {
        PersonSet GetPersonSet(string personSetName);
        RepoDataSet GetRepoDataSet();

        Task ReloadAsync(CancellationToken cancellationToken);
    }
}
