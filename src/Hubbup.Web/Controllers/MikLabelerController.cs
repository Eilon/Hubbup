using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hubbup.Web.DataSources;
using Hubbup.Web.ML;
using Hubbup.Web.Utils;
using Hubbup.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Hubbup.Web.Controllers
{
    [Route("miklabel")]
    [Authorize(AuthenticationSchemes = "Cookies")]
    public class MikLabelerController : Controller
    {
        private readonly IDataSource _dataSource;
        private readonly ILogger<MikLabelerController> _logger;
        private static readonly string ModelPath = "/ML/GitHubLabelerModel.zip";
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
            //MLHelper.BuildAndTrainModel(_hostingEnvironment.ContentRootPath + "/ML/issueData.tsv", _hostingEnvironment.ContentRootPath + ModelPath, MyTrainerStrategy.OVAAveragedPerceptronTrainer);

            var existingAreaLabels =
                (await gitHub.Issue.Labels.GetAllForRepository("aspnet", "AspNetCore"))
                .Where(label => label.Name.StartsWith("area-", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var excludeAllAreaLabelsQuery =
                string.Join(
                    " ",
                    existingAreaLabels.Select(label => $"-label:\"{label.Name}\""));

            var getIssuesRequest = new SearchIssuesRequest(excludeAllAreaLabelsQuery)
            {
                Is = new[] { IssueIsQualifier.Issue, IssueIsQualifier.Open },
                Repos = new RepositoryCollection
                {
                    { "aspnet", "AspNetCore" }
                },
            };

            var issueSearchResult = await gitHub.Search.SearchIssues(getIssuesRequest);

            var labeler = new Labeler(_hostingEnvironment.ContentRootPath + ModelPath);
            var predictionList = new List<LabelSuggestion>();

            foreach (var issue in issueSearchResult.Items)
            {
                var prediction = labeler.PredictLabel(issue);
                predictionList.Add(new LabelSuggestion
                {
                    Issue = issue,
                    Prediction = prediction,
                    AreaLabel = existingAreaLabels.Single(label => string.Equals(label.Name, prediction.Area, StringComparison.OrdinalIgnoreCase)),
                });
            }

            return View(new MikLabelViewModel
            {
                PredictionList = predictionList,
                TotalIssuesFound = issueSearchResult.TotalCount,
            });
        }

        [HttpPost]
        [Route("ApplyLabel")]
        public async Task<IActionResult> ApplyLabel(int issueNumber, string prediction)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var gitHub = GitHubUtils.GetGitHubClient(accessToken);
            var issue = await gitHub.Issue.Get("aspnet", "aspnetcore", issueNumber);

            await ApplyPredictedLabel(issue, prediction, gitHub);
            return RedirectToAction("Index");
        }

        private async Task ApplyPredictedLabel(Issue issue, string label, IGitHubClient client)
        {
            var issueUpdate = new IssueUpdate();
            issueUpdate.AddLabel(label);

            await client.Issue.Update("aspnet", "aspnetcore", issue.Number, issueUpdate);
        }
    }
}
