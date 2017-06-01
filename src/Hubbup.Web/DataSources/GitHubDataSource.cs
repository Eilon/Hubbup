using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Hubbup.Web.Models;
using Hubbup.Web.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Hubbup.Web.DataSources
{
    public class GitHubDataSource : IGitHubDataSource
    {
        private const string GraphQlEndPoint = "https://api.github.com/graphql";
        private const int PageSize = 10;
        private const int AssigneeBatchSize = 5;
        private const int LabelBatchSize = 5;
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

        public async Task<SearchResults<IReadOnlyList<IssueData>>> SearchIssuesAsync(string query, string accessToken)
        {
            var queryRequest = new GraphQlQueryRequest(Queries.SearchIssues);
            queryRequest.Variables["searchQuery"] = query;
            queryRequest.Variables["pageSize"] = PageSize;
            queryRequest.Variables["assigneeBatchSize"] = AssigneeBatchSize;
            queryRequest.Variables["labelBatchSize"] = LabelBatchSize;
            queryRequest.Variables["cursor"] = null;

            var issues = new List<IssueData>();
            var pageIndex = 0;

            var data = default(SearchResults<Dtos.ConnectionResult<Dtos.Issue>>);
            var rateLimitInfo = new RateLimitInfo();
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
                var result = JsonConvert.DeserializeObject<Dtos.GraphQlResult<SearchResults<Dtos.ConnectionResult<Dtos.Issue>>>>(json, _settings);
                if (result.Errors != null && result.Errors.Any())
                {
                    throw new InvalidOperationException(result.Errors.First().Message);
                }
                data = result.Data;

                // Add rate limit info
                _logger.LogTrace("Request completed, consumed {cost} of rate limit {limit}. Remaining: {remaining}, resets at {resetAt}",
                    data.RateLimit.Cost,
                    data.RateLimit.Limit,
                    data.RateLimit.Remaining,
                    data.RateLimit.ResetAt);
                rateLimitInfo = RateLimitInfo.Add(rateLimitInfo, data.RateLimit);

                foreach (var issue in data.Search.Nodes)
                {
                    var issueData = new IssueData()
                    {
                        IsPr = string.Equals(issue.Type, "PullRequest", StringComparison.Ordinal),
                        Url = issue.Url,
                        Number = issue.Number,
                        Repository = issue.Repository,
                        Title = issue.Title,
                        Author = issue.Author,
                        Milestone = issue.Milestone,
                        CreatedAt = issue.CreatedAt.ToPacificTime(),
                        UpdatedAt = issue.UpdatedAt.ToPacificTime(),
                        CommentCount = issue.Comments.TotalCount
                    };

                    // Log a warning if there are labels or assignees beyond the ones we fetched
                    // We could make additional requests to fetch these if we find we need them.
                    if (issue.Labels.PageInfo.HasNextPage)
                    {
                        _logger.LogWarning("Issue {owner}/{repo}#{issueNumber} has more than the limit of {limit} labels.", issue.Repository.Owner.Name, issue.Repository.Name, issue.Number, AssigneeBatchSize);
                    }
                    if (issue.Assignees.PageInfo.HasNextPage)
                    {
                        _logger.LogWarning("Issue {owner}/{repo}#{issueNumber} has more than  the limit of {limit} assignees.", issue.Repository.Owner.Name, issue.Repository.Name, issue.Number, LabelBatchSize);
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

            return new SearchResults<IReadOnlyList<IssueData>>(issues, rateLimitInfo);
        }

        private static class Queries
        {
            public static readonly string SearchIssues = @"
query SearchIssues($searchQuery: String!, $pageSize: Int!, $assigneeBatchSize: Int!, $labelBatchSize: Int!, $cursor: String) {
  rateLimit {
    limit,
    remaining,
    cost,
    resetAt
  },
  search(first: $pageSize, query: $searchQuery, after: $cursor, type: ISSUE) {
    issueCount,
    pageInfo {
      endCursor,
      hasNextPage,
    },
    nodes {
      type: __typename,
      ... on Node {
        id,
      },
      ... on UniformResourceLocatable {
        url,
      },
      ... on RepositoryNode {
        repository {
          id,
          name,
          owner {
            id,
            url,
            login,
            avatarUrl,
          },
        },
      },
      ... on Comment {
        author {
          ... on User {
            id,
            url,
            login,
            name,
            avatarUrl
          },
        },
        createdAt,
        updatedAt,
      },
      ... on Assignable {
        assignees(first: $assigneeBatchSize){
          nodes {
            id,
            url,
            login,
            name,
            avatarUrl,
          },
          pageInfo {
            endCursor,
            hasNextPage,
          },
        },
      }
      ... on Labelable {
 			  labels(first: $labelBatchSize) {
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
      },
      ... on PullRequest {
        number,
        title,
        comments {
          totalCount,
        }
      },
      ... on Issue {
        number,
        milestone {
          id,
          title,
        },
        title,
        comments {
          totalCount,
        },
      },
    },
  },
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
                public string Type { get; set; }
                public string Url { get; set; }
                public int Number { get; set; }
                public RepositoryReference Repository { get; set; }
                public string Title { get; set; }
                public UserReference Author { get; set; }
                public Milestone Milestone { get; set; }
                public DateTimeOffset CreatedAt { get; set; }
                public DateTimeOffset UpdatedAt { get; set; }
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
