using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hubbup.Web.Models
{
    public class IssueData
    {
        public int Number { get; set; }
        public RepositoryReference Repository { get; set; }
        public string Title { get; set; }
        public UserReference Author { get; set; }
        public Milestone Milestone { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int CommentCount { get; set; }

        public IList<UserReference> Assignees { get; } = new List<UserReference>();
        public IList<Label> Labels { get; } = new List<Label>();
    }
}
