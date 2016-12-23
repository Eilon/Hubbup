using System;

namespace Hubbup.Web.Models
{
    public class RepoFailure
    {
        public RepoDefinition Repo { get; set; }
        public string FailureMessage { get; set; }
        public Exception Exception { get; set; }
    }
}
