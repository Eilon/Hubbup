using System.Collections.Generic;

namespace ProjectKIssueList.Models
{
    public class RepoDefinition
    {
        public RepoDefinition(string org, string name)
        {
            Org = org;
            Name = name;
        }
        public string Org { get; set; }
        public string Name { get; set; }
    }

    public interface IRepoSetProvider
    {
        IDictionary<string, RepoDefinition[]> GetRepoSetLists();

        RepoDefinition[] GetAllRepos();

        RepoDefinition[] GetRepoSet(string repoSet);

        bool RepoSetExists(string repoSet);
    }
}
