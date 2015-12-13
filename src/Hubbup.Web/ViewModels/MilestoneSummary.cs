using System.Collections.Generic;
using Hubbup.Web.Models;

namespace Hubbup.Web.ViewModels
{

    public class MilestoneSummary
    {
        public RepoDefinition Repo { get; set; }

        public ICollection<MilestoneData> MilestoneData { get; set; }
    }
}
