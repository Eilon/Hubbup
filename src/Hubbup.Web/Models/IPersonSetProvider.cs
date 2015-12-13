namespace Hubbup.Web.Models
{
    public interface IPersonSetProvider
    {
        PersonSet GetPersonSet(string personSetName);
    }
}
