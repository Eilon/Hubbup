using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Hubbup.MikLabelModel
{
    public class MikLabelerProvider
    {
        private readonly ConcurrentDictionary<string, MikLabelerModel> _mikLabelers = new ConcurrentDictionary<string, MikLabelerModel>();
        private readonly ILogger<MikLabelerProvider> _logger;

        public MikLabelerProvider(ILogger<MikLabelerProvider> logger)
        {
            _logger = logger;
        }

        public MikLabelerModel GetMikLabeler(IMikLabelerPathProvider pathProvider)
        {
            var path = pathProvider.GetModelPath();
            return _mikLabelers.GetOrAdd(
                path,
                p =>
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var model = new MikLabelerModel(p);
                    stopwatch.Stop();
                    _logger.LogInformation("Creating new MikLabelerModel for path {PATH} in {TIME}ms", p, stopwatch.ElapsedMilliseconds);
                    return model;
                });
        }
    }
}
