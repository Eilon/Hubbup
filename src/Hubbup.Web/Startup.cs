using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Hubbup.IssueMoverApi;
using Hubbup.IssueMoverClient;
using Hubbup.MikLabelModel;
using Hubbup.Web.DataSources;
using Hubbup.Web.Diagnostics.Metrics;
using Hubbup.Web.Diagnostics.Telemetry;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

            if (HostingEnvironment.IsDevelopment())
            {
                services.Configure<LocalJsonDataSourceOptions>(Configuration.GetSection("LocalJson"));
                services.AddSingleton<IDataSource, LocalJsonDataSource>();
            }
            else
            {
                services.Configure<RemoteJsonDataSourceOptions>(Configuration.GetSection("RemoteJson"));
                services.AddSingleton<IDataSource, RemoteJsonDataSource>();
            }

            services.AddSingleton<IGitHubDataSource, GitHubDataSource>();
            services.AddSingleton<IHostedService, DataLoadingService>();

            services.AddMemoryCache();

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

            services
                .AddMetrics(options =>
                {
                    options.FlushRate = TimeSpan.FromSeconds(5);
                })
                .AddApplicationInsights();
            services.AddSingleton<IRequestTelemetryListener, ApplicationInsightsRequestTelemetryListener>();

            services.AddSingleton<MikLabelerProvider>();

            // Add Application Insights services for Snapshot Debugger
            // https://docs.microsoft.com/en-us/azure/azure-monitor/app/snapshot-debugger-vm?toc=/azure/azure-monitor/toc.json#configure-snapshot-collection-for-aspnet-core-20-applications
            services.Configure<SnapshotCollectorConfiguration>(Configuration.GetSection(nameof(SnapshotCollectorConfiguration)));
            services.AddSingleton<ITelemetryProcessorFactory>(sp => new SnapshotCollectorTelemetryProcessorFactory(sp));
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseDiagnostics();

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

        private class SnapshotCollectorTelemetryProcessorFactory : ITelemetryProcessorFactory
        {
            private readonly IServiceProvider _serviceProvider;

            public SnapshotCollectorTelemetryProcessorFactory(IServiceProvider serviceProvider) =>
                _serviceProvider = serviceProvider;

            public ITelemetryProcessor Create(ITelemetryProcessor next)
            {
                var snapshotConfigurationOptions = _serviceProvider.GetService<IOptions<SnapshotCollectorConfiguration>>();
                return new SnapshotCollectorTelemetryProcessor(next, configuration: snapshotConfigurationOptions.Value);
            }
        }
    }
}
