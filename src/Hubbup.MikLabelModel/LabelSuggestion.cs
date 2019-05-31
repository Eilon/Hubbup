using System.Collections.Generic;
using Octokit;

namespace Hubbup.MikLabelModel
{
    public class LabelSuggestion
    {
        public Issue Issue { get; set; }
        public List<LabelAreaScore> LabelScores { get; set; }
    }
}