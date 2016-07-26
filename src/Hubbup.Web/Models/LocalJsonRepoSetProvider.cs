using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Hubbup.Web.Models
{
    public class LocalJsonRepoSetProvider : IRepoSetProvider
    {
        private Dictionary<string, RepoSetDefinition> _repoSetList;

        public LocalJsonRepoSetProvider(
            IOptions<LocalJsonRepoSetProviderOptions> localJsonRepoSetProviderOptions,
            IHostingEnvironment hostingEnvironment,
            IApplicationLifetime applicationLifetime,
            ILogger<LocalJsonRepoSetProvider> logger)
        {
            PhysicalJsonFilePath = Path.Combine(hostingEnvironment.ContentRootPath, localJsonRepoSetProviderOptions.Value.JsonFilePath);

            // Prime the data immediately
            UpdateDataSet();

            // Then keep updating the data...
            var applicationStoppingCancellationToken = applicationLifetime.ApplicationStopping;

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
                            UpdateDataSet();
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(
                                eventId: 1,
                                exception: ex,
                                message: "The repo settings file {JsonFilePath} could not be read",
                                args: PhysicalJsonFilePath);
                        }
                    }
                },
                applicationStoppingCancellationToken);
        }

        private void UpdateDataSet()
        {
            using (var fileStream = File.OpenText(PhysicalJsonFilePath))
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

        public string PhysicalJsonFilePath { get; }

        public RepoDataSet GetRepoDataSet()
        {
            return new RepoDataSet(_repoSetList);
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
