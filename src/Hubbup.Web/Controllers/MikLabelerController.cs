using System.Collections.Generic;
using System.Threading.Tasks;
using Hubbup.Web.DataSources;
using Hubbup.Web.Utils;
using Hubbup.Web.ML;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Octokit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.IO;
using System;

namespace Hubbup.Web.Controllers
{
    [Route("miklabel")]
    [Authorize(AuthenticationSchemes = "Cookies")]
    public class MikLabelerController : Controller
    {
        private readonly IDataSource _dataSource;
        private readonly ILogger<MikLabelerController> _logger;
        private static string AppPath => Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
        private static string ModelPath = "/ML/GitHubLabelerModel.zip";
        private static string ModelFilePathName = $"GitHubLabelerModel.zip";
        public enum MyTrainerStrategy : int { SdcaMultiClassTrainer = 1, OVAAveragedPerceptronTrainer = 2 };
        public static IConfiguration Configuration { get; set; }
        private readonly IHostingEnvironment _hostingEnvironment;

        public MikLabelerController(
            IDataSource dataSource,
            ILogger<MikLabelerController> logger,
            IHostingEnvironment hostingEnvironment)
        {
            _dataSource = dataSource;
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
        }

        [Route("")]
        public async Task<IActionResult> Index(string repoSet)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);

            //This line re-trains the ML Model
            //MLHelper.BuildAndTrainModel(_hostingEnvironment.ContentRootPath + "/ML/issueData.tsv", _hostingEnvironment.ContentRootPath + ModelFilePathName, MyTrainerStrategy.OVAAveragedPerceptronTrainer);

            //2. Try/test to predict a label for a single hard-coded Issue
            var labeler = new Labeler(_hostingEnvironment.ContentRootPath + ModelPath);

            var issues = await gitHub.Issue.GetAllForRepository("aspnet", "AspNetCore", new ApiOptions { PageSize = 100, PageCount = 1 });

            var issueList = new List<Issue>();
            var predictionLabels = new List<GitHubIssuePrediction>();
            foreach (Issue issue in issues)
            {
                if (issue.PullRequest != null)
                {
                    continue;
                }

                var areaLabel = findAreaLabel(issue);
                if (areaLabel == null)
                {
                    issueList.Add(issue);
                    var predictedLabel = labeler.PredictLabel(issue);
                    predictionLabels.Add(predictedLabel);
                }
            }

            ViewData["Predictions"] = predictionLabels;
            return View(issueList);
        }

        [HttpPost]
        [Route("ApplyLabel")]
        public async Task<IActionResult> ApplyLabel(int issueNumber, string prediction)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);
            var issue = await gitHub.Issue.Get("aspnet", "aspnetcore", issueNumber);
            ApplyPredictedLabel(issue, prediction, gitHub);
            return RedirectToAction("Index");
        }

        private void ApplyPredictedLabel(Issue issue, string label, IGitHubClient client)
        {
            var issueUpdate = new IssueUpdate();
            issueUpdate.AddLabel(label);

            client.Issue.Update("aspnet", "aspnetcore", issue.Number, issueUpdate);

            Console.WriteLine($"Issue {issue.Number} : \"{issue.Title}\" \t was labeled as: {label}");
        }

        private static Label findAreaLabel(Issue issue)
        {
            var labels = issue.Labels;
            foreach (Label label in labels)
            {
                if (label.Name.StartsWith("area-"))
                {
                    return label;
                }
            }

            //No area label
            return null;
        }
    }
}
