# Hubbup

ASP.NET Core 1 to view GitHub issues for team stand-up

# Setup

1. Clone the repo
2. Register the app in GitHub: https://github.com/settings/applications/new
 1. Check the VS project properties for the SSL URL to use as the Homepage URL
 2. Use `https://localhost:[port]/signin-github` as the callback URL for GitHub (where `[port]` is the SSL port for the site)
3. Use the `dotnet user-secrets` tool to add a token called `GitHubClientId` with the client ID and a token called `GitHubClientSecret` with the client secret
4. Run the app
