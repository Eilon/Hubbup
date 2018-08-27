using System;
using System.Collections.Generic;

namespace Hubbup.IssueMover.Dto
{
    public class MilestoneCreateResult : IErrorResult
    {
        public string MilestoneCreated { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }

    public class MilestoneCreateRequest
    {
        public string Milestone { get; set; }
    }
}
