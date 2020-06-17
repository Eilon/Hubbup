using Hubbup.Web.Models;
using System.Collections.Generic;

namespace Hubbup.Web.ViewModels
{

    public class MilestoneSummary
    {
        public RepoDefinition Repo { get; set; }

        public ICollection<MilestoneData> MilestoneData { get; set; }
    }
}
