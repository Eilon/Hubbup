using System;
using Octokit;

namespace ProjectKIssueList.Models
{
    public class IssueWithRepo
    {
        public Issue Issue { get; set; }
        public RepoDefinition Repo { get; set; }
        public DateTimeOffset? WorkingStartTime { get; set; }
        public bool IsInAssociatedPersonSet { get; set; }
    }
}
