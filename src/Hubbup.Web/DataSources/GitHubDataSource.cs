using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Hubbup.Web.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Hubbup.Web.DataSources
{
    public class GitHubDataSource : IGitHubDataSource
    {
        private const string GraphQlEndPoint = "https://api.github.com/graphql";

        private static readonly JsonSerializerSettings _settings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None
        };

        private readonly HttpClient _client = new HttpClient();
        private readonly ILogger<GitHubDataSource> _logger;

        public GitHubDataSource(ILogger<GitHubDataSource> logger)
        {
            _logger = logger;
        }

        public async Task<IReadOnlyList<IssueData>> SearchIssuesAsync(string query, string accessToken)
        {
            var queryRequest = new GraphQlQueryRequest(Queries.SearchIssues);
            queryRequest.Variables["searchQuery"] = query;
            queryRequest.Variables["pageSize"] = 100;
            queryRequest.Variables["cursor"] = null;

            var issues = new List<IssueData>();
            var pageIndex = 0;

            var data = default(Dtos.SearchResult<Dtos.ConnectionResult<Dtos.Issue>>);
            do
            {
                if (data != null)
                {
                    queryRequest.Variables["cursor"] = data.Search.PageInfo.EndCursor;
                }

                var req = new HttpRequestMessage(HttpMethod.Post, GraphQlEndPoint);
                req.Headers.UserAgent.Add(new ProductInfoHeaderValue("hubbup.io", Startup.Version));
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var json = JsonConvert.SerializeObject(queryRequest, _settings);

                req.Content = new StringContent(json);
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                _logger.LogTrace("Requesting page {pageIndex} of search results from GitHub for query '{query}'", pageIndex, query);
                var resp = await _client.SendAsync(req);
                resp.EnsureSuccessStatusCode();

                json = await resp.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<Dtos.GraphQlResult<Dtos.SearchResult<Dtos.ConnectionResult<Dtos.Issue>>>>(json, _settings);
                if (result.Errors != null && result.Errors.Any())
                {
                    throw new InvalidOperationException(result.Errors.First().Message);
                }
                data = result.Data;

                foreach (var issue in data.Search.Nodes)
                {
                    var issueData = new IssueData()
                    {
                        Number = issue.Number,
                        Repository = issue.Repository,
                        Title = issue.Title,
                        Author = issue.Author,
                        Milestone = issue.Milestone,
                        CreatedAt = issue.CreatedAt,
                        UpdatedAt = issue.UpdatedAt,
                        CommentCount = issue.Comments.TotalCount
                    };

                    // Log a warning if there are labels or assignees beyond the 10 we fetched
                    if (issue.Labels.PageInfo.HasNextPage)
                    {
                        _logger.LogWarning("Issue {owner}/{repo}#{issueNumber} has more than 10 labels. Only the first 10 are fetched.", issue.Repository.Owner.Name, issue.Repository.Name, issue.Number);
                    }
                    if (issue.Assignees.PageInfo.HasNextPage)
                    {
                        _logger.LogWarning("Issue {owner}/{repo}#{issueNumber} has more than 10 assignees. Only the first 10 are fetched.", issue.Repository.Owner.Name, issue.Repository.Name, issue.Number);
                    }

                    // Load the assignees and labels
                    foreach (var assignee in issue.Assignees.Nodes)
                    {
                        issueData.Assignees.Add(assignee);
                    }

                    foreach (var label in issue.Labels.Nodes)
                    {
                        issueData.Labels.Add(label);
                    }

                    // Add this to the list of issues
                    issues.Add(issueData);
                }

                pageIndex += 1;
            } while (data.Search.PageInfo.HasNextPage);

            return issues;
        }

        private static class Queries
        {
            public static readonly string SearchIssues = @"
query SearchIssues($searchQuery: String!, $pageSize: Int!, $cursor: String) {
  search(first: $pageSize, query: $searchQuery, after: $cursor, type: ISSUE) {
    nodes {
      ... on Issue {
        number,
        repository {
          id,
          name,
          owner {
            id,
            login,
            avatarUrl,
          },
        },
        title,
        author {
          ... on User {
            id,
            name,
            avatarUrl
          },
        },
        milestone {
          id
          title,
        },
        createdAt,
        updatedAt,
        assignees(first: 10){
          nodes {
            id,
            name,
            avatarUrl,
          },
          pageInfo {
            endCursor,
            hasNextPage,
          },
        },
        labels(first: 10) {
          nodes {
            id,
            name,
            color
          },
          pageInfo {
            endCursor,
            hasNextPage,
          },
        },
        comments {
          totalCount,
        },
      },
    },
    pageInfo {
      endCursor,
      hasNextPage,
    },
  },
  rateLimit {
    limit,
    remaining,
    cost,
    resetAt
  }
}
".Trim().Replace("\r", "").Replace("\n", "").Replace("  ", " ");
        }

        private class Dtos
        {
            public class GraphQlResult<T>
            {
                public T Data { get; set; }
                public IEnumerable<GraphQlError> Errors { get; set; }
            }

            public class GraphQlError
            {
                public string Message { get; set; }
                public IEnumerable<GraphQlErrorLocation> Locations { get; set; }
            }

            public class GraphQlErrorLocation
            {
                public int Line { get; set; }
                public int Column { get; set; }
            }

            public class SearchResult<T>
            {
                public T Search { get; set; }
                public RateLimit RateLimit { get; set; }
            }

            public class RateLimit
            {
                public int Limit { get; set; }
                public int Remaining { get; set; }
                public int Cost { get; set; }
                public DateTime ResetAt { get; set; }
            }

            public class ConnectionResult<T>
            {
                public T[] Nodes { get; set; }
                public PageInfo PageInfo { get; set; }
            }

            public class PageInfo
            {
                public string EndCursor { get; set; }
                public bool HasNextPage { get; set; }
            }

            public class Issue
            {
                public int Number { get; set; }
                public RepositoryReference Repository { get; set; }
                public string Title { get; set; }
                public UserReference Author { get; set; }
                public Milestone Milestone { get; set; }
                public DateTime CreatedAt { get; set; }
                public DateTime UpdatedAt { get; set; }
                public ConnectionResult<UserReference> Assignees { get; set; }
                public ConnectionResult<Label> Labels { get; set; }
                public Comments Comments { get; set; }
            }

            public class Comments
            {
                public int TotalCount { get; set; }
            }
        }
    }
}
