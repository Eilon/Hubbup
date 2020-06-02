using System.Collections.Generic;

// Various models used to deserialize GraphQL responses from GitHub
namespace CreateMikLabelModel
{
    public class GitHubIssueListPage
    {
        public bool IsError { get; set; }
        public Data Issues { get; set; }
    }

    public class Data
    {
        public Repository Repository { get; set; }
    }

    public class Repository
    {
        public string Name { get; set; }
        public Issues Issues { get; set; }
    }

    public class Issues
    {
        public List<IssuesNode> Nodes { get; set; }
        public PageInfo PageInfo { get; set; }
        public long TotalCount { get; set; }
    }

    public class IssuesNode
    {
        public long Number { get; set; }
        public string Title { get; set; }
        public string BodyText { get; set; }
        public Labels Labels { get; set; }
    }

    public class Labels
    {
        public List<LabelsNode> Nodes { get; set; }
        public long TotalCount { get; set; }
    }

    public class LabelsNode
    {
        public string Name { get; set; }
    }

    public class PageInfo
    {
        public bool HasNextPage { get; set; }
        public string EndCursor { get; set; }
    }
}
