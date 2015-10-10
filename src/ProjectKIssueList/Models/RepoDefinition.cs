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
}
