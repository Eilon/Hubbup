using System;
using System.Collections.Generic;
using System.Linq;

namespace Hubbup.Web.Services
{
    public class RepoSetNames
    {
        public static readonly string DefaultRepoSetName = "AspNetCore";

        public static readonly IList<RepoSet> RepoSets = new List<RepoSet>()
        {
            new RepoSet("AspNetCore", new[]
            {
                ("dotnet", "aspnetcore"),
            }),
            new RepoSet("MAUI", new[]
            {
                ("dotnet", "maui"),
            }),
        };

        public static RepoSet GetReposInSet(string repoSetName)
        {
            repoSetName = string.IsNullOrEmpty(repoSetName) ? DefaultRepoSetName : repoSetName;

            return RepoSets.FirstOrDefault(repoSet => string.Equals(repoSet.Name, repoSetName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class RepoSet
    {
        public RepoSet(string name, IList<(string owner, string repo)> repos)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Repos = repos ?? throw new ArgumentNullException(nameof(repos));
        }

        public string Name {  get; }
        public IList<(string owner, string repo)> Repos { get; }
    }
}
