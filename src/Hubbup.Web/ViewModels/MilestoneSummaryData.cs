using System.Collections.Generic;

namespace Hubbup.Web.ViewModels
{
    public class MilestoneSummaryData : List<MilestoneSummary>
    {
        public List<MilestoneSummary> MilestoneData { get; set; }
        public List<string> MilestonesAvailable { get; set; }
    }
}
