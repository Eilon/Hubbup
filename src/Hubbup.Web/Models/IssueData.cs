using System;
using System.Collections.Generic;
using Hubbup.Web.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Hubbup.Web.Models
{
    public class IssueData
    {
        public string Id { get; set; }
        public bool IsPr { get; set; }
        public bool Working { get; set; }
        public string Url { get; set; }
        public int Number { get; set; }
        public RepositoryReference Repository { get; set; }
        public string Title { get; set; }
        public UserReference Author { get; set; }
        public Milestone Milestone { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? WorkingStartedAt { get; set; }
        public int CommentCount { get; set; }

        public bool Stale => WorkingStartedAt != null ?
            WorkingStartedAt.Value < DateTimeOffset.UtcNow.AddDays(-14) :
            UpdatedAt < DateTimeOffset.UtcNow.AddDays(-14);
        
        public string CreatedTimeAgo => CreatedAt.ToTimeAgo();
        public string WorkingTimeAgo => WorkingStartedAt?.ToTimeAgo();

        public IList<UserReference> Assignees { get; } = new List<UserReference>();
        public IList<Label> Labels { get; } = new List<Label>();
    }
}
