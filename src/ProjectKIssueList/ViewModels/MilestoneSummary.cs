using System.Collections.Generic;
using ProjectKIssueList.Models;

namespace ProjectKIssueList.ViewModels
{

    public class MilestoneSummary
    {
        public RepoDefinition Repo { get; set; }

        public ICollection<MilestoneData> MilestoneData { get; set; }
    }
}
