using System.Collections.Generic;
using Hubbup.MikLabelModel;
using Octokit;

namespace Hubbup.Web.ViewModels
{
    public class LabelSuggestionViewModel
    {
        public Issue Issue { get; set; }
        public List<(LabelAreaScore, Label)> LabelScores { get; set; }
        public string RepoOwner { get; set; }
        public string RepoName { get; set; }
    }
}
