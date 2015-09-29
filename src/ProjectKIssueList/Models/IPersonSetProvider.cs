namespace ProjectKIssueList.Models
{
    public interface IPersonSetProvider
    {
        PersonSet GetPersonSet(string personSetName);
    }

    public class PersonSet
    {
        public string[] People { get; set; }
    }
}
