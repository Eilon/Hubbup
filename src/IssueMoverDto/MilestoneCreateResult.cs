using System;
using System.Collections.Generic;

namespace Hubbup.IssueMover.Dto
{
    public class MilestoneCreateResult
    {
        public string MilestoneCreated { get; set; }
    }

    public class MilestoneCreateRequest
    {
        public string Milestone { get; set; }
    }
}
