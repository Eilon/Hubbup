using CreateMikLabelModel.DL;
using CreateMikLabelModel.ML;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CreateMikLabelModel
{
    public class Program
    {
        private static readonly (string owner, string repo)[][] Repos = new[]
        {
            new[] {
                ("dotnet", "aspnetcore"),   // first item is the target Repo
            },
            new[] {
                ("dotnet", "extensions"),
            },
            new[] {
                ("dotnet", "runtime"),      // first item is the target Repo
                ("dotnet", "extensions"),   // the rest are archived repositories
                ("dotnet", "corefx"),
                ("dotnet", "coreclr"),
                ("dotnet", "core-setup"),
            }
        };

        static async Task<int> Main()
        {
            string folder = Directory.GetCurrentDirectory();
            foreach (var repoCombo in Repos)
            {
                string customFilenamePrefix = $"{repoCombo[0].owner}-{repoCombo[0].repo}-";
                var issueFiles = new DataFilePaths(folder, customFilenamePrefix, forPrs: false);
                var prFiles = new DataFilePaths(folder, customFilenamePrefix, forPrs: true);

                if (await DownloadHelper.DownloadItemsAsync(issueFiles.InputPath, repoCombo) == -1)
                    return -1;

                var dm = new DatasetModifier(targetRepo: repoCombo[0].repo);
                Console.WriteLine($"Reading input TSV {issueFiles.InputPath}...");
                await DatasetHelper.PrepareAndSaveDatasetsForIssuesAsync(issueFiles, dm);
                await DatasetHelper.PrepareAndSaveDatasetsForPrsAsync(prFiles, dm);

                var mlHelper = new MLHelper();

                Console.WriteLine($"First train issues");
                mlHelper.Train(issueFiles, forPrs: false);
                
                Console.WriteLine($"Next to train PRs");
                mlHelper.Train(prFiles, forPrs: true);

                mlHelper.Test(issueFiles, forPrs: false);
                mlHelper.Test(prFiles, forPrs: true);

                Console.WriteLine(new string('-', 80));
                Console.WriteLine();
            }

            Console.WriteLine($"Please remember to copy the ZIP files to the web site's ML folder");

            return 0;
        }
    }
}
