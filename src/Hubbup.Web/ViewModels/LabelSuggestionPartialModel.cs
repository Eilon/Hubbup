using Hubbup.Web.Services;
using Octokit;

namespace Hubbup.Web.ViewModels
{
    public class LabelSuggestionPartialModel
    {
        public string RepoOwner { get; set; }
        public string RepoName { get; set; }
        public Issue Issue { get; set; }

        /// <summary>
        /// The desired label predicted by the service, even though this label might not exist (anymore) in the repo.
        /// </summary>
        public string DesiredLabel { get; set; }

        public Label Label { get; set; }
        public LabelAreaScore Score { get; set; }
        public int Index { get; set; }
        public bool IsBestPrediction { get; set; }

        public string RepoSetName { get; set; }
    }
}
