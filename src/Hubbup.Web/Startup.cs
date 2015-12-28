using Microsoft.AspNet.Authentication;
using Microsoft.AspNet.Authentication.Cookies;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using Hubbup.Web.Models;

namespace Hubbup.Web
{
    public class Startup
    {
        public Startup(IHostingEnvironment env, IApplicationEnvironment appEnv)
        {
            // Set up configuration sources
            var builder = new ConfigurationBuilder(appEnv.ApplicationBasePath)
                .AddJsonFile("config.json")
                .AddEnvironmentVariables();

            if (env.IsDevelopment())
            {
                builder.AddUserSecrets();
            }

            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; set; }

        // This method gets called by the runtime.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddInstance<IRepoSetProvider>(new StaticRepoSetProvider());
            services.AddInstance<IPersonSetProvider>(new StaticPersonSetProvider());

            services.AddCaching();

            services.AddSession();

            services.AddAuthentication();

            services.Configure<SharedAuthenticationOptions>(options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });

            services.AddMvc();
        }

        // Configure is called after ConfigureServices is called.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.MinimumLevel = LogLevel.Information;
            loggerFactory.AddConsole();

            if (env.IsDevelopment())
            {
                app.UseErrorPage();
            }
            else
            {
                app.UseErrorHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseCookieAuthentication(options =>
            {
                options.AutomaticAuthentication = true;
                options.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.LoginPath = new PathString("/signin");
            });

            app.UseGitHubAuthentication(options =>
            {
                options.ClientId = Configuration["GitHubClientId"];
                options.ClientSecret = Configuration["GitHubClientSecret"];
                options.Scope.Add("repo");
                options.SaveTokensAsClaims = true;
            });

            app.UseSession();

            app.UseMvc();
        }
    }
}
