using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hubbup.Web.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Hubbup.Web.DataSources
{
    public abstract class JsonDataSource : IDataSource
    {
        protected static readonly Task<ReadFileResult> UnchangedReadResultTask = Task.FromResult(new ReadFileResult());

        private volatile RepoDataSet _repoDataSet = RepoDataSet.Empty;
        private volatile Dictionary<string, PersonSet> _personSets = new Dictionary<string, PersonSet>();

        private volatile string _repoEtag = null;
        private volatile string _personSetEtag = null;

        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IApplicationLifetime _applicationLifetime;
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly object _reloadLock = new object();

        public JsonDataSource(
            IHostingEnvironment hostingEnvironment,
            IApplicationLifetime applicationLifetime,
            ILogger<JsonDataSource> logger,
            TelemetryClient telemetryClient)
        {
            _hostingEnvironment = hostingEnvironment;
            _applicationLifetime = applicationLifetime;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        public RepoDataSet GetRepoDataSet() => _repoDataSet;

        public PersonSet GetPersonSet(string personSetName) => _personSets[personSetName];

        protected abstract Task<ReadFileResult> ReadJsonStream(string fileName, string etag);

        public async Task ReloadAsync(CancellationToken cancellationToken)
        {
            await Task.WhenAll(ReloadRepoSets(), ReloadPersonSets());
        }

        private async Task ReloadPersonSets()
        {
            _logger.LogTrace("Reloading repoSets.json ...");
            var getDataStopWatch = new Stopwatch();
            getDataStopWatch.Start();

            using (var result = await ReadJsonStream("personSet.json", _personSetEtag))
            {
                if (result.Changed)
                {
                    using (var jsonTextReader = new JsonTextReader(result.Content))
                    {
                        var jsonSerializer = new JsonSerializer();
                        var data = jsonSerializer.Deserialize<IDictionary<string, PersonSetDto>>(jsonTextReader);

                        var dict = data.ToDictionary(
                            pair => pair.Key,
                            pair => new PersonSet(pair.Value.People));

                        // Atomically assign the entire data set
                        _repoEtag = result.Etag;
                        _personSets = dict;
                    }
                    _logger.LogInformation("Reloaded person sets");
                }
                else
                {
                    _logger.LogInformation("Skipped reloading person set, nothing changed.");
                }
            }

            getDataStopWatch.Stop();

            var getDataEventTelemetry = new EventTelemetry
            {
                Name = "UpdatePersonSets",
            };
            getDataEventTelemetry.Properties.Add("durationInMilliseconds", getDataStopWatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            _telemetryClient.TrackEvent(getDataEventTelemetry);
            _logger.LogTrace("Reloaded repoSets.json in {durationInMilliseconds} milliseconds", getDataStopWatch.ElapsedMilliseconds);
        }

        private async Task ReloadRepoSets()
        {
            _logger.LogTrace("Reloading repoSets.json ...");
            var getDataStopWatch = new Stopwatch();
            getDataStopWatch.Start();

            using (var result = await ReadJsonStream("repoSet.json", _personSetEtag))
            {
                if (result.Changed)
                {
                    using (var jsonTextReader = new JsonTextReader(result.Content))
                    {
                        var jsonSerializer = new JsonSerializer();
                        var data = jsonSerializer.Deserialize<IDictionary<string, RepoSetDto>>(jsonTextReader);

                        var repoSetList = data.ToDictionary(
                            pair => pair.Key,
                            pair => CreateRepoSetDefinition(pair.Value));

                        // Atomically assign the entire data set
                        _repoEtag = result.Etag;
                        _repoDataSet = new RepoDataSet(repoSetList);
                    }
                    _logger.LogInformation("Reloaded repo sets");
                }
                else
                {
                    _logger.LogInformation("Skipped reloading repo sets, nothing changed.");
                }
            }

            getDataStopWatch.Stop();

            var getDataEventTelemetry = new EventTelemetry
            {
                Name = "UpdateRepoSets",
            };
            getDataEventTelemetry.Properties.Add("durationInMilliseconds", getDataStopWatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            _telemetryClient.TrackEvent(getDataEventTelemetry);
            _logger.LogTrace("Reloaded repoSets.json in {durationInMilliseconds} milliseconds", getDataStopWatch.ElapsedMilliseconds);
        }

        private static RepoSetDefinition CreateRepoSetDefinition(RepoSetDto repoInfo)
        {
            return new RepoSetDefinition
            {
                AssociatedPersonSetName = repoInfo.AssociatedPersonSetName,
                LabelFilter = repoInfo.LabelFilter,
                WorkingLabels = repoInfo.WorkingLabels,
                RepoExtraLinks = repoInfo.RepoExtraLinks != null
                    ? repoInfo.RepoExtraLinks
                        .Select(extraLink => new RepoExtraLink
                        {
                            Title = extraLink.Title,
                            Url = extraLink.Url,
                        })
                        .ToList()
                    : new List<RepoExtraLink>(),
                Repos = repoInfo.Repos
                    .Select(repoDef => new RepoDefinition(repoDef.Org, repoDef.Repo, (RepoInclusionLevel)Enum.Parse(typeof(RepoInclusionLevel), repoDef.InclusionLevel, ignoreCase: true)))
                    .ToArray(),
            };
        }

        protected struct ReadFileResult : IDisposable
        {
            public bool Changed { get; }
            public TextReader Content { get; }
            public string Etag { get; }

            public ReadFileResult(TextReader content, string etag)
            {
                Changed = true;
                Content = content;
                Etag = etag;
            }

            public void Dispose()
            {
                Content.Dispose();
            }
        }

        private class PersonSetDto
        {
            public string[] People { get; set; }
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

        private class RepoExtraLinkDto
        {
            public string Title { get; set; }

            public string Url { get; set; }
        }
    }
}
