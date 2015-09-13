using System.Collections.Generic;

namespace ProjectKIssueList.Models
{
    public interface IRepoSetProvider
    {
        IDictionary<string, string[]> GetRepoSetLists();

        string[] GetAllRepos();

        string[] GetRepoSet(string repoSet);

        bool RepoSetExists(string repoSet);
    }
}
