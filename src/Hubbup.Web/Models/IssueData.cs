using System;
using System.Collections.Generic;

namespace Hubbup.Web.Models
{
    public class IssueData
    {
        public IssueType Type { get; set; }
        public string Url { get; set; }
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

        internal static IssueType ParseType(string type)
        {
            if (string.Equals(type, "PullRequest", StringComparison.Ordinal))
            {
                return IssueType.PullRequest;
            }
            else if (string.Equals(type, "Issue", StringComparison.Ordinal))
            {
                return IssueType.Issue;
            }
            throw new FormatException($"Unknown Issue type: {type}");
        }
    }
}
