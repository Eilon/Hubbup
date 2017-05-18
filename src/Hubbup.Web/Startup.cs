using System.IO;
using System.Reflection;
using Hubbup.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
            services.Configure<RemoteJsonRepoSetProviderOptions>(options =>
            {
                options.JsonFileUrl = "https://raw.githubusercontent.com/Eilon/Hubbup-data/master/hubbup-data.json";
            });
            services.AddSingleton<IRepoSetProvider, RemoteJsonRepoSetProvider>();

            services.AddSingleton<IPersonSetProvider>(new StaticPersonSetProvider());

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
            });

            services.AddOAuthAuthentication("GitHub", options =>
            {
                options.CallbackPath = new PathString("/signin-github");
                options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                options.UserInformationEndpoint = "https://api.github.com/user";
                options.ClaimsIssuer = "GitHub";
                options.DisplayName = "GitHub";

                options.ClientId = Configuration["Authentication:GitHub:ClientId"];
                options.ClientSecret = Configuration["Authentication:GitHub:ClientSecret"];
                options.Scope.Add("repo");
                options.SaveTokens = true;
            });

            services.AddMvc(options =>
            {
                if (HostingEnvironment.IsDevelopment())
                {
                    options.SslPort = 44347;
                }
            });
        }

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
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
