using System;

namespace Hubbup.Web.Models
{
    public class RepoDefinition : IComparable<RepoDefinition>, IComparable, IEquatable<RepoDefinition>
    {
        public RepoDefinition(string owner, string name, RepoInclusionLevel repoInclusionLevel)
        {
            Owner = owner;
            Name = name;
            RepoInclusionLevel = repoInclusionLevel;
        }

        public string Owner { get; set; }
        public string Name { get; set; }
        public RepoInclusionLevel RepoInclusionLevel { get; set; }

        public int CompareTo(RepoDefinition other)
        {
            if (other == null)
            {
                return 1;
            }
            return
                StringComparer.OrdinalIgnoreCase.Compare(
                    Owner + "/" + Name,
                    other.Owner + "/" + other.Name);
        }

        public int CompareTo(object obj)
        {
            var otherRepoDefinition = obj as RepoDefinition;
            if (otherRepoDefinition == null)
            {
                return 1;
            }
            return CompareTo(otherRepoDefinition);
        }

        public override bool Equals(object obj)
        {
            return CompareTo(obj) == 0;
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Owner + "/" + Name);
        }

        public bool Equals(RepoDefinition other)
        {
            return CompareTo(other) == 0;
        }
    }
}
