using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;
using System.Security.Claims;
using Hubbup.Web.DataSources;
using Hubbup.Web.Diagnostics.Metrics;
using Hubbup.Web.Diagnostics.Telemetry;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Blazor.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Hubbup.Web
{
    public class Startup
    {
        public static readonly string Version = typeof(Startup).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        private const string GitHubAuth = "GitHub";

        public IConfiguration Configuration { get; }
        public IHostingEnvironment HostingEnvironment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
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
            services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService, DataLoadingService>();

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
                    options.LoginPath = "/signin";
                    options.Cookie.Name = "HubbupAuthCookie";
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

                        context.Identity.AddClaim(new Claim("gh-scopes", GitHubScopeString));

                        var payload = JObject.Parse(await response.Content.ReadAsStringAsync());
                        context.RunClaimActions(payload);
                    };
                });

            services.AddMvc()
                .AddRazorPagesOptions(r =>
                {
                    r.Conventions.AuthorizeFolder("/");
                });

            services
                .AddMetrics(options =>
                {
                    options.FlushRate = TimeSpan.FromSeconds(5);
                })
                .AddApplicationInsights();
            services.AddSingleton<IRequestTelemetryListener, ApplicationInsightsRequestTelemetryListener>();

            services.AddResponseCompression(options =>
            {
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                {
                    MediaTypeNames.Application.Octet,
                    WasmMediaTypeNames.Application.Wasm,
                });
            });
        }

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory, IDataSource dataSource, IApplicationLifetime lifetime)
        {
            // Load data before we start things up.
            // Until https://github.com/aspnet/Hosting/issues/1088 is resolved, this has to synchronously block.
            var logger = loggerFactory.CreateLogger<Startup>();
            logger.LogDebug("Loading repo set and person set data...");
            dataSource.ReloadAsync(lifetime.ApplicationStopping).Wait();
            logger.LogDebug("Loaded repo set and person set data");

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

            app.UseAuthentication();

            app.UseMvc();

            app.UseBlazor<IssueMoverClient.Program>();
        }
    }
}
