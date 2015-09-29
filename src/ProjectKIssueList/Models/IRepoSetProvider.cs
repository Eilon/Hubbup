using System.Collections.Generic;

namespace ProjectKIssueList.Models
{
    public class RepoDefinition
    {
        public RepoDefinition(string owner, string name)
        {
            Owner = owner;
            Name = name;
        }
        public string Owner { get; set; }
        public string Name { get; set; }
    }

    public interface IRepoSetProvider
    {
        IDictionary<string, RepoSetDefinition> GetRepoSetLists();

        RepoSetDefinition GetRepoSet(string repoSet);

        bool RepoSetExists(string repoSet);
    }
}
