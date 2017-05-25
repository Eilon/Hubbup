using System.Collections.Generic;

namespace Hubbup.Web.Models
{
    public class RepoSetDefinition
    {
        public string BaseQuery { get; set; }

        public RepoDefinition[] Repos { get; set; }

        public string AssociatedPersonSetName { get; set; }

        public string[] WorkingLabels { get; set; }

        public string LabelFilter { get; set; }

        public List<RepoExtraLink> RepoExtraLinks { get; set; }
    }
}
