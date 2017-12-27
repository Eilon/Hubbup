namespace Hubbup.Web.Models
{
    public enum RepoInclusionLevel
    {
        /// <summary>
        /// The repo is not in the repo set.
        /// </summary>
        NotInRepoSet,

        /// <summary>
        /// Show all issues and PRs in the repo, regardless of assignee.
        /// </summary>
        AllItems,

        /// <summary>
        /// Show only issues and PRs with assignees within the person set.
        /// </summary>
        ItemsAssignedToPersonSet,

        /// <summary>
        /// Ignore all issues and PRs in the repo set.
        /// </summary>
        Ignored,
    }
}
