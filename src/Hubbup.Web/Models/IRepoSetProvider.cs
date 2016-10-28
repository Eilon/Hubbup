using System.Threading.Tasks;

namespace Hubbup.Web.Models
{
    public interface IRepoSetProvider
    {
        Task<RepoDataSet> GetRepoDataSet();
    }
}
