using System.Collections.Generic;
using System.Linq;

namespace Hubbup.Web.Models
{
    public class RepoSetDefinition
    {
        public RepoDefinition[] Repos { get; set; }

        public string AssociatedPersonSetName { get; set; }

        public HashSet<string> WorkingLabels { get; set; }

        public string LabelFilter { get; set; }

        public List<RepoExtraLink> RepoExtraLinks { get; set; }

        public string GenerateQuery(params string[] additionalFields)
        {
            return string.Join(" ", Enumerable.Concat(
                Repos.Select(r => $"repo:{r.Owner}/{r.Name}"),
                additionalFields));
        }
    }
}
