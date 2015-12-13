using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Authentication;
using Microsoft.AspNet.Mvc;

namespace Hubbup.Web.Controllers
{
    public class GitHubAuthDataAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var gitHubAccessToken = context.HttpContext.Session.GetString("GitHubAccessToken");
            var gitHubName = context.HttpContext.Session.GetString("GitHubName");

            // If session state didn't have our data, either there's no one logged in, or they just logged in
            // but the claims haven't yet been read.
            if (string.IsNullOrEmpty(gitHubAccessToken))
            {
                if (!context.HttpContext.User.Identity.IsAuthenticated)
                {
                    // Not authenticated at all? Go to GitHub to authorize the app
                    context.Result = new ChallengeResult(
                        authenticationScheme: "GitHub",
                        properties: new AuthenticationProperties { RedirectUri = "/" });
                    return;
                }

                // Authenticated but haven't read the claims? Process the claims
                gitHubAccessToken = context.HttpContext.User.FindFirst("access_token")?.Value;
                gitHubName = context.HttpContext.User.Identity.Name;
                context.HttpContext.Session.SetString("GitHubAccessToken", gitHubAccessToken);
                context.HttpContext.Session.SetString("GitHubName", gitHubName);
            }

            context.ActionArguments.Add("gitHubAccessToken", gitHubAccessToken);
            context.ActionArguments.Add("gitHubName", gitHubName);
        }
    }
}
