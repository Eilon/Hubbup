using System.Collections.Generic;

namespace Hubbup.Web.ViewModels
{
    public class MikLabelViewModel
    {
        public List<LabelSuggestion> PredictionList { get; set; }
        public int TotalIssuesFound { get; set; }
    }
}
