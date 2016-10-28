using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Hubbup.Web.Models
{
    public abstract class JsonRepoSetProvider : IRepoSetProvider
    {
        private Dictionary<string, RepoSetDefinition> _repoSetList;

        private IApplicationLifetime ApplicationLifetime { get; }
        public IHostingEnvironment HostingEnvironment { get; }
        public ILogger<JsonRepoSetProvider> Logger { get; }

        public JsonRepoSetProvider(
            IHostingEnvironment hostingEnvironment,
            IApplicationLifetime applicationLifetime,
            ILogger<JsonRepoSetProvider> logger)
        {
            HostingEnvironment = hostingEnvironment;
            ApplicationLifetime = applicationLifetime;
            Logger = logger;
        }

        public async Task<RepoDataSet> GetRepoDataSet()
        {

            // Prime the data immediately
            await UpdateDataSet();

            // Then keep updating the data in the background...
            StartStuff();

            return new RepoDataSet(_repoSetList);
        }

        private void StartStuff()
        {
            var applicationStoppingCancellationToken = ApplicationLifetime.ApplicationStopping;

            Task.Run(
                async () =>
                {
                    while (!applicationStoppingCancellationToken.IsCancellationRequested)
                    {
                        //await Task.Delay(TimeSpan.FromSeconds(15), applicationStoppingCancellationToken);
                        await Task.Delay(TimeSpan.FromMinutes(5), applicationStoppingCancellationToken);

                        if (applicationStoppingCancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        // do work
                        try
                        {
                            await UpdateDataSet();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(
                                eventId: 1,
                                exception: ex,
                                message: "The repo settings file could not be read");
                        }
                    }
                },
                applicationStoppingCancellationToken);
        }

        protected abstract Task<TextReader> GetJsonStream();

        private async Task UpdateDataSet()
        {
            using (var fileStream = await GetJsonStream())
            using (var jsonTextReader = new JsonTextReader(fileStream))
            {
                var jsonSerializer = new JsonSerializer();
                var data = jsonSerializer.Deserialize<IDictionary<string, RepoSetDto>>(jsonTextReader);

                var repoSetList = new Dictionary<string, RepoSetDefinition>(StringComparer.OrdinalIgnoreCase);

                foreach (var repoInfo in data)
                {
                    var repoSetDefinition = new RepoSetDefinition
                    {
                        AssociatedPersonSetName = repoInfo.Value.AssociatedPersonSetName,
                        LabelFilter = repoInfo.Value.LabelFilter,
                        WorkingLabels = repoInfo.Value.WorkingLabels,
                        RepoExtraLinks = repoInfo.Value.RepoExtraLinks != null
                            ? repoInfo.Value.RepoExtraLinks
                                .Select(extraLink => new RepoExtraLink
                                {
                                    Title = extraLink.Title,
                                    Url = extraLink.Url,
                                })
                                .ToList()
                            : new List<RepoExtraLink>(),
                        Repos = repoInfo.Value.Repos
                            .Select(repoDef => new RepoDefinition(repoDef.Org, repoDef.Repo, (RepoInclusionLevel)Enum.Parse(typeof(RepoInclusionLevel), repoDef.InclusionLevel, ignoreCase: true)))
                            .ToArray(),
                    };
                    repoSetList.Add(repoInfo.Key, repoSetDefinition);
                }

                // Atomically assign the entire data set
                _repoSetList = repoSetList;
            }
        }

        private class RepoSetDto
        {
            public string AssociatedPersonSetName { get; set; }
            public string[] WorkingLabels { get; set; }
            public string LabelFilter { get; set; }
            public RepoExtraLinkDto[] RepoExtraLinks { get; set; }
            public RepoInfoDto[] Repos { get; set; }
        }

        private class RepoInfoDto
        {
            public string Org { get; set; }
            public string Repo { get; set; }
            public string InclusionLevel { get; set; }

        }

        public class RepoExtraLinkDto
        {
            public string Title { get; set; }

            public string Url { get; set; }
        }
    }
}
