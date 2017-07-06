using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using Hubbup.Web.DataSources;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Hubbup.Web
{
    public class Startup
    {
        public static readonly string Version = typeof(Startup).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        public IConfiguration Configuration { get; }
        public IHostingEnvironment HostingEnvironment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
        {
            Configuration = configuration;
            HostingEnvironment = hostingEnvironment;
        }

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
            services.AddSingleton<IHostedService, DataLoadingService>();

            services.AddMemoryCache();

            services.AddAuthentication(options =>
            {
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = "GitHub";
            });

            services.AddCookieAuthentication(options =>
            {
                options.LoginPath = new PathString("/signin");

                // Work around https://github.com/aspnet/Security/issues/1231
                options.CookieSameSite = SameSiteMode.None;
            });

            services.AddOAuthAuthentication("GitHub", options =>
            {
                options.CallbackPath = new PathString("/signin-github");
                options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                options.UserInformationEndpoint = "https://api.github.com/user";
                options.ClaimsIssuer = "GitHub";

                options.ClientId = Configuration["GitHubClientId"];
                options.ClientSecret = Configuration["GitHubClientSecret"];
                options.Scope.Add("repo");
                options.SaveTokens = true;

                options.Events.OnCreatingTicket = async context =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                    var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();

                    var payload = JObject.Parse(await response.Content.ReadAsStringAsync());

                    // Add GitHub claims
                    context.Identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, payload.Value<string>("id"), context.Options.ClaimsIssuer));
                    context.Identity.AddClaim(new Claim(ClaimTypes.Name, payload.Value<string>("login"), context.Options.ClaimsIssuer));
                    //context.Identity.AddClaim(new Claim(ClaimTypes.Email, payload.Value<string>("email"), context.Options.ClaimsIssuer));
                    //context.Identity.AddClaim(new Claim("urn:github:name", payload.Value<string>("name"), context.Options.ClaimsIssuer));
                    context.Identity.AddClaim(new Claim("urn:github:url", payload.Value<string>("url"), context.Options.ClaimsIssuer));
                };
            });

            services.AddMvc(options =>
            {
                if (HostingEnvironment.IsDevelopment())
                {
                    options.SslPort = 44347;
                }
            });
        }

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory, IDataSource dataSource, IApplicationLifetime lifetime)
        {
            // Load data before we start things up.
            // Until https://github.com/aspnet/Hosting/issues/1088 is resolved, this has to synchronously block.
            var logger = loggerFactory.CreateLogger<Startup>();
            logger.LogInformation("Loading repo set and person set data...");
            dataSource.ReloadAsync(lifetime.ApplicationStopping).Wait();
            logger.LogInformation("Loaded repo set and person set data");

            if (HostingEnvironment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseMvc();
        }
    }
}
