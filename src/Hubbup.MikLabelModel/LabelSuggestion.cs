using System.Collections.Generic;
using Octokit;

namespace Hubbup.MikLabelModel
{
    public class LabelSuggestion
    {
        public List<LabelAreaScore> LabelScores { get; set; }
    }
}