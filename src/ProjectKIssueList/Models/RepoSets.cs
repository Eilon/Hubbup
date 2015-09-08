using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectKIssueList.Models
{
    public static class RepoSets
    {
        private static readonly Dictionary<string, string[]> RepoSetList = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "kcore",
                new string[] {
                    "aspnet-docker",
                    "BasicMiddleware",
                    "Caching",
                    "Coherence",
                    "Coherence-Signed",
                    "CoreCLR",
                    "DataProtection",
                    "dnvm",
                    "dnx",
                    "Entropy",
                    "FileSystem",
                    "Helios",
                    "homebrew-dnx",
                    "Hosting",
                    "HttpAbstractions",
                    "HttpClient",
                    "KestrelHttpServer",
                    "Logging",
                    "Proxy",
                    "ResponseCaching",
                    "Roslyn",
                    "Security",
                    "ServerTests",
                    "Session",
                    "Setup",
                    "Signing",
                    "StaticFiles",
                    "Universe",
                    "UserSecrets",
                    "WebListener",
                    "WebSockets",
                }
            },
            {
                "mvc",
                new string[] {
                    "Antiforgery",
                    "aspnet.xunit",
                    "Common",
                    "CORS",
                    "DependencyInjection",
                    "Diagnostics",
                    "DnxTools",
                    "EventNotification",
                    "jquery-ajax-unobtrusive",
                    "jquery-validation-unobtrusive",
                    "Localization",
                    "MusicStore",
                    "Mvc",
                    "Razor",
                    "RazorTooling",
                    "Routing",
                    "Testing",
                }
            },
            {
                "data",
                new string[] {
                    "Configuration",
                    "DataCommon",
                    "EntityFramework",
                    "EntityFramework.Docs",
                    "Identity",
                    "Microsoft.Data.Sqlite",
                    "Options",
                    "SqlClient",
                }
            },
            {
                "pm",
                new string[] {
                    "Docs",
                    "Docs-internal",
                    "Home",
                    "NerdDinner",
                    "PackageSearch",
                }
            },
            {
                "engineering",
                new string[] {
                    "Announcements",
                    "BugTracker",
                    "EndToEnd",
                    "External",
                    "IBC",
                    "kbot",
                    "KExpense",
                    "MusicStore-Authorization",
                    "Perf",
                    "Performance",
                    "Reporting",
                    "specs",
                    "Stress",
                    "TeamCityTrigger",
                    "xunit",
                }
            },
        };

        public static IDictionary<string, string[]> GetRepoSetLists()
        {
            return RepoSetList;
        }

        public static string[] GetAllRepos()
        {
            return RepoSetList.SelectMany(repoSet => repoSet.Value).ToArray();
        }

        public static string[] GetRepoSet(string repoSet)
        {
            return RepoSetList[repoSet];
        }

        public static bool HasRepoSet(string repoSet)
        {
            return RepoSetList.ContainsKey(repoSet);
        }
    }
}
