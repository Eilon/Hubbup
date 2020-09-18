using Hubbup.Web.Services;
using Octokit;
using System.Collections.Generic;

namespace Hubbup.Web.ViewModels
{
    public class LabelSuggestionViewModel
    {
        public Issue Issue { get; set; }
        public List<(LabelAreaScore, Label)> LabelScores { get; set; }
        public string RepoOwner { get; set; }
        public string RepoName { get; set; }

        public string ErrorMessage { get; set; }
    }
}
