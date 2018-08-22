using System;
using System.Collections.Generic;

namespace Hubbup.IssueMover.Dto
{
    public class LabelCreateResult
    {
        public List<LabelData> LabelsCreated { get; set; }
    }

    public class LabelCreateRequest
    {
        public List<LabelData> Labels { get; set; }
    }
}
