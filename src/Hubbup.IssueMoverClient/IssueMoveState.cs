using Hubbup.IssueMover.Dto;

namespace Hubbup.IssueMoverClient
{
    public class IssueMoveState
    {
        public IssueMoveStateType StateType { get; set; }
        public string Link { get; set; }
        public string Description { get; set; }
        public string Result { get; set; }
        public IErrorResult ErrorResult { get; set; }
        public bool Success { get; set; }
    }
}
