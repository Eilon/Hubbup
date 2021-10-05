using Octokit;

namespace Hubbup.Web.ViewModels
{
    public class LabelViewModel
    {
        public LabelViewModel(Label label, string desiredLabel)
        {
            Label = label;
            DesiredLabel = desiredLabel;
        }

        public Label Label { get; }
        public string DesiredLabel { get; }
    }
}
