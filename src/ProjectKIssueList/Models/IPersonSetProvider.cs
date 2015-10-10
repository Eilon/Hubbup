namespace ProjectKIssueList.Models
{
    public interface IPersonSetProvider
    {
        PersonSet GetPersonSet(string personSetName);
    }
}
