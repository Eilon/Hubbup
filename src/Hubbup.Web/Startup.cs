using Hubbup.IssueMoverApi;
using Hubbup.IssueMoverClient;
using Hubbup.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubbup.Web
{
    public class Startup
    {
        public static readonly string Version = typeof(Startup).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        private const string GitHubAuth = "GitHub";
        private const string GitHubScopesClaim = "gh-scopes";

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment HostingEnvironment { get; }

        public Startup(IConfiguration configuration, IWebHostEnvironment hostingEnvironment)
        {
            Configuration = configuration;
            HostingEnvironment = hostingEnvironment;
        }

        public static readonly string[] GitHubScopes = new[]
        {
            "repo",
            "read:org",
        };
        public static readonly string GitHubScopeString = string.Join(", ", GitHubScopes);

        public void ConfigureServices(IServiceCollection services)
        {
            // Blazor start
            services.AddScoped<AppState>();
            services.AddScoped<IIssueMoverService, IssueMoverLocalService>();
            services.AddScoped<IGitHubAccessor, AspNetGitHubAccessor>();
            services.AddServerSideBlazor();
            // Blazor end

            services.AddOptions();

            services.AddMemoryCache();

            if (!HostingEnvironment.IsDevelopment())
            {
                services.AddSignalR().AddAzureSignalR(options =>
                {
                    options.ServerStickyMode = Microsoft.Azure.SignalR.ServerStickyMode.Required;
                });
            }

            services
                .AddAuthentication(options =>
                {
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = GitHubAuth;
                })
                .AddCookie(options =>
                {
                    options.Cookie.Name = "HubbupAuthCookie";
                    options.Events = new CookieAuthenticationEvents()
                    {
                        OnValidatePrincipal = context =>
                        {
                            // If scope requirements change then make everyone log back in
                            var scopeClaim = context.Principal.FindFirst(GitHubScopesClaim);
                            if (!string.Equals(scopeClaim?.Value, GitHubScopeString, StringComparison.Ordinal))
                            {
                                context.RejectPrincipal();
                            }

                            return Task.CompletedTask;
                        }
                    };
                })
                .AddOAuth(GitHubAuth, options =>
                {
                    options.CallbackPath = "/signin-github";
                    options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                    options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                    options.UserInformationEndpoint = "https://api.github.com/user";
                    options.ClaimsIssuer = "GitHub";

                    options.ClientId = Configuration["GitHubClientId"];
                    options.ClientSecret = Configuration["GitHubClientSecret"];
                    foreach (var ghScope in GitHubScopes)
                    {
                        options.Scope.Add(ghScope);
                    }
                    options.SaveTokens = true;
                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
                    options.ClaimActions.MapJsonKey("urn:github:url", "url");

                    options.Events.OnCreatingTicket = async context =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                        var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                        response.EnsureSuccessStatusCode();

                        context.Identity.AddClaim(new Claim(GitHubScopesClaim, GitHubScopeString));

                        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
                        context.RunClaimActions(payload);
                    };
                });

            services.AddMvc()
                .AddRazorPagesOptions(r =>
                {
                    r.Conventions.AuthorizeFolder("/");
                })
                .AddNewtonsoftJson();

            services.AddSingleton<MikLabelService>();

            services.AddHttpClient();
        }

        public void Configure(IApplicationBuilder app)
        {
            if (HostingEnvironment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(routes =>
            {
                routes.MapControllers();
                routes.MapRazorPages();
                routes.MapBlazorHub();
            });
        }
    }
}
