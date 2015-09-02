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
                    "SignalR-Client-Cpp",
                    "SignalR-Client-Java",
                    "SignalR-Client-JS",
                    "SignalR-Client-Net",
                    "SignalR-Redis",
                    "SignalR-Server",
                    "SignalR-ServiceBus",
                    "SignalR-SqlServer",
                    "Signing",
                    "StaticFiles",
                    "UserSecrets",
                    "WebListener",
                    "WebSockets",
                }
            },
            {
                "mvc",
                new string[] {
                    "Antiforgery",
                    "Common",
                    "CORS",
                    "DependencyInjection",
                    "Diagnostics",
                    "EventNotification",
                    "jquery-ajax-unobtrusive",
                    "jquery-validation-unobtrusive",
                    "Localization",
                    "MusicStore",
                    "Mvc",
                    "Razor",
                    "RazorTooling",
                    "Routing",
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
                    "aspnet.xunit",
                    "BugTracker",
                    "Coherence",
                    "Coherence-Signed",
                    "DnxTools",
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
                    "Testing",
                    "Universe",
                    "xunit",
                }
            },
        };

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
