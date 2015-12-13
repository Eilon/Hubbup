namespace Hubbup.Web.Models
{
    public class RepoSetDefinition
    {
        public RepoDefinition[] Repos { get; set; }

        public string AssociatedPersonSetName { get; set; }

        public string WorkingLabel { get; set; }

        public string LabelFilter { get; set; }
    }
}
