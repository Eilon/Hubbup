using System;
using System.Collections.Generic;

namespace Hubbup.IssueMover.Dto
{
    public class LabelCreateResult : IErrorResult
    {
        public List<LabelData> LabelsCreated { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }
}
