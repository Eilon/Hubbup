using Octokit;
using System.Collections.Generic;

namespace Hubbup.Web.ViewModels
{
    public class MikLabelViewModel
    {
        public List<LabelSuggestionViewModel> PredictionList { get; set; }
        public int TotalIssuesFound { get; set; }
        public List<Label> AllAreaLabels { get; set; }
    }
}
