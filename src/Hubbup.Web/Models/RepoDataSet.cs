using System.Collections.Generic;

namespace Hubbup.Web.Models
{
    public class RepoDataSet
    {
        public static readonly RepoDataSet Empty = new RepoDataSet(new Dictionary<string, RepoSetDefinition>());

        private readonly IDictionary<string, RepoSetDefinition> _repoSetList;

        public RepoDataSet(IDictionary<string, RepoSetDefinition> repoSetList)
        {
            _repoSetList = repoSetList;
        }

        public IDictionary<string, RepoSetDefinition> GetRepoSetLists()
        {
            return _repoSetList;
        }

        public RepoSetDefinition GetRepoSet(string repoSet)
        {
            return _repoSetList.TryGetValue(repoSet, out var result) ? result : null;
        }

        public bool RepoSetExists(string repoSet)
        {
            return _repoSetList.ContainsKey(repoSet);
        }
    }
}
