using System.Collections.Generic;

namespace ProjectKIssueList.Models
{
    public interface IRepoSetProvider
    {
        IDictionary<string, RepoSetDefinition> GetRepoSetLists();

        RepoSetDefinition GetRepoSet(string repoSet);

        bool RepoSetExists(string repoSet);
    }
}
