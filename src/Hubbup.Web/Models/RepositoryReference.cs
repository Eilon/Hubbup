namespace Hubbup.Web.Models
{
    public class RepositoryReference
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public UserReference Owner { get; set;  }
    }
}
