using System;

namespace CreateMikLabelModel.ML
{
    public class DatasetModifier
    {
        private readonly string _targetRepo;
        public DatasetModifier(string targetRepo)
        {
            _targetRepo = targetRepo;
        }

        /// <summary>
        /// allows modifying the area label prior to saving into the dataset
        /// </summary>
        public Func<string, string, string> ReMapLabel => _targetRepo.Equals("runtime") ? Remapper.RenameLabelForRuntimeRepo : Remapper.NoAreaLabelChange;

        /// <summary>
        /// allows modifying file paths prior to saving into the dataset
        /// </summary>
        public Func<string[], string, string[]> ReMapFiles => _targetRepo.Equals("runtime") ? Remapper.RemapFilesForRuntimeRepo : Remapper.NoFileChanges;

        private static class Remapper
        {
            public static Func<string, string, string> NoAreaLabelChange = (originalArea, fromRepo) => originalArea;
            public static Func<string[], string, string[]> NoFileChanges = (filePaths, repoFrom) => filePaths;

            public static readonly Func<string, string, string> RenameLabelForRuntimeRepo = (originalArea, fromRepo) =>
                fromRepo switch
                {
                    "coreclr" => originalArea switch
                    {
                        "area-Build" => "area-Infrastructure-coreclr",
                        _ => originalArea,
                    },
                    "extensions" => RenameFromExtensionsToRuntime(originalArea),
                    _ => originalArea,
                };

            private static string RenameFromExtensionsToRuntime(string predictedLabel)
            {
                var ret = predictedLabel;
                switch (predictedLabel)
                {
                    case "area-caching":
                        ret = "area-Extensions-Caching";
                        break;
                    case "area-config":
                        ret = "area-Extensions-Configuration";
                        break;
                    case "area-dependencyinjection":
                        ret = "area-Extensions-DependencyInjection";
                        break;
                    case "area-filesystem":
                        ret = "area-Extensions-FileSystem";
                        break;
                    case "area-hosting":
                        ret = "area-Extensions-Hosting";
                        break;
                    case "area-httpclientfactory":
                        ret = "area-Extensions-HttpClientFactory";
                        break;
                    case "area-logging":
                        ret = "area-Extensions-Logging";
                        break;
                    case "area-options":
                        ret = "area-Extensions-Options";
                        break;
                    case "area-primitives":
                        ret = "area-Extensions-Primitives";
                        break;
                    case "area-healthchecks":
                    case "area-platform":
                    case "area-infrastructure":
                    case "area-mvc":
                    case "area-localization":
                        ret = "";
                        break;
                    default:
                        break;
                }
                return ret;
            }

            public static Func<string[], string, string[]> RemapFilesForRuntimeRepo = (filePaths, repoFrom) =>
            {
                for (var i = 0; i < filePaths.Length; i++)
                {
                    switch (repoFrom)
                    {
                        case "corefx":
                        case "coreclr":
                        case "core-setup":
                            if (filePaths[i].StartsWith($"src/coreclr/", StringComparison.Ordinal))
                            {
                                filePaths[i] = $"src/coreclr/src/" + filePaths[i].Substring(
                                    $"src/coreclr/".Length);
                            }
                            if (filePaths[i].Contains($"src/coreclr/src/mscorlib/shared/", StringComparison.Ordinal))
                            {
                                filePaths[i] = filePaths[i].Replace(
                                    $"src/coreclr/src/mscorlib/shared/",
                                    $"src/libraries/System.Private.CoreLib/src/");
                            }
                            if (filePaths[i].Contains($"src/coreclr/System.Private.CoreLib/shared", StringComparison.Ordinal))
                            {
                                filePaths[i] = filePaths[i].Replace(
                                    $"src/coreclr/src/System.Private.CoreLib/shared/",
                                    $"src/libraries/System.Private.CoreLib/src/");
                            }
                            else if (filePaths[i].Contains($".azure-ci.yml", StringComparison.Ordinal))
                            {
                                filePaths[i] = filePaths[i].Replace(
                                    $".azure-ci.yml",
                                    $"eng/pipelines/" + repoFrom + $"/.azure-ci.yml");
                            }
                            else if (filePaths[i].Contains($"azure-pipelines.yml", StringComparison.Ordinal))
                            {
                                filePaths[i] = filePaths[i].Replace(
                                    $"azure-pipelines.yml",
                                    $"eng/pipelines/" + repoFrom + $"/azure-pipelines.yml");
                            }
                            else if (filePaths[i].Contains($"eng/pipelines/", StringComparison.Ordinal))
                            {
                                filePaths[i] = filePaths[i].Replace(
                                    $"eng/pipelines",
                                    $"eng/pipelines/" + repoFrom);
                            }
                            break;
                        case "extensions":
                        case "Extensions":
                            var prefix = "src/libraries/Microsoft.Extensions";
                            foreach (var item in new (string from, string to)[] { 
                                // area-Caching
                                    ("Caching/Abstraction", "Caching.Abstractions"),
                                // area-Configuration
                                    ("Configuration/Config.Abstractions", "Configuration.Abstractions"),
                                    ("Configuration/Config.Binder", "Configuration.Binder"),
                                    ("Configuration/Config.CommandLine", "Configuration.CommandLine"),
                                    ("Configuration/Config.EnvironmentVariables", "Configuration.EnvironmentVariables"),
                                    ("Configuration/Config.FileExtensions", "Configuration.FileExtensions"),
                                    ("Configuration/Config.Ini", "Configuration.Ini"),
                                    ("Configuration/Config.Json", "Configuration.Json"),
                                    ("Configuration/Config.Xml", "Configuration.Xml"),
                                    ("Configuration/Config", "Configuration"),
                                // area-DependencyInjection
                                    ("DependencyInjection/DI.Abstractions", "DependencyInjection.Abstractions"),
                                    ("DependencyInjection/DI", "DependencyInjection"),
                                // area-FileSystems
                                    ("FileProviders/Abstractions", "FileProviders.Abstractions"), ////////// TODO confirm
                                    ("FileProviders/Composite", "FileProviders.Composite"),
                                    ("FileProviders/Physical", "FileProviders.Physical"),
                                    ("FileSystemGlobbing", "FileSystemGlobbing"),
                                // area-Hosting
                                    ("Hosting/Abstractions", "Hosting.Abstractions"),
                                    ("Hosting/Hosting", "Hosting"),
                                // area-HttpClientFactory
                                    ("HttpClientFactory", "Http"),
                                // area-Logging
                                    ("Logging/Logging.Abstractions", "Logging.Abstractions"),
                                    ("Logging/Logging.Configuration", "Logging.Configuration"),
                                    ("Logging/Logging.Console", "Logging.Console"),
                                    ("Logging/Logging.Debug", "Logging.Debug"),
                                    ("Logging/Logging.EventLog", "Logging.EventLog"),
                                    ("Logging/Logging.EventSource", "Logging.EventSource"),
                                    ("Logging/Logging.TraceSource", "Logging.TraceSource"),
                                    ("Logging/Logging", "Logging"),
                                // area-Options
                                    ("Options/ConfigurationExtensions", "Options.ConfigurationExtensions"),
                                    ("Options/DataAnnotations", "Options.DataAnnotations"),
                                    ("Options/Options", "Options"),
                                // area-Primitives
                                    ("Primitives", "Primitives"),
                                })
                            {
                                if (filePaths[i].StartsWith($"src/{item.from}/", StringComparison.Ordinal))
                                {
                                    filePaths[i] = filePaths[i].Replace($"src/{item.from}/", $"{prefix}.{item.to}/");
                                    break;
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
                // switch on coreclr, corefx, core-setup, extensions, etc.
                return filePaths;
            };
        }
    }
}
