using Hubbup.Web.Models;
using Octokit;
using System;
using System.Collections.Generic;

namespace Hubbup.Web.ViewModels
{
    public class IssueListViewModel
    {
        public List<RepoFailure> RepoFailures { get; set; }

        public string GitHubUserName { get; set; }
        public string LastUpdated { get; set; }
        public List<RepoExtraLink> ExtraLinks { get; set; }

        public string RepoSetName { get; set; }
        public string[] RepoSetNames { get; set; }
        public int TotalIssues { get; set; }
        public int UntriagedIssues { get; set; }
        public int UnassignedIssues { get; set; }
        public int WorkingIssues { get; set; }
        public int OpenPullRequests { get; set; }
        public int StalePullRequests { get; set; }

        public List<RepoSummary> MainReposIncluded { get; set; }
        public List<RepoSummary> ExtraReposIncluded { get; set; }
        public MilestoneSummaryData MainMilestoneSummary { get; set; }
        public MilestoneSummaryData ExtraMilestoneSummary { get; set; }
        public IList<MissingRepoSet> MissingRepos { get; set; }
        public IList<MissingRepoSet> IgnoredRepos { get; set; }

        public string OpenIssuesQuery { get; set; }
        public string UntriagedIssuesQuery { get; set; }
        public string UnassignedIssuesQuery { get; set; }
        public string WorkingIssuesQuery { get; set; }
        public string OpenPRsQuery { get; set; }
        public string StalePRsQuery { get; set; }

        public GroupByAssigneeViewModel GroupByAssignee { get; set; }

        public TimeSpan PageRequestTime { get; set; }
        public ApiInfo LastApiInfo { get; set; }
    }
}
