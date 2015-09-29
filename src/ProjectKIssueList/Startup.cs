using Microsoft.AspNet.Authentication;
using Microsoft.AspNet.Authentication.Cookies;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using ProjectKIssueList.Models;

namespace ProjectKIssueList
{
    public class Startup
    {
        public Startup(IHostingEnvironment env, IApplicationEnvironment appEnv)
        {
            // Setup configuration sources.
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

            // Add MVC services to the services container.
            services.AddMvc();
        }

        // Configure is called after ConfigureServices is called.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.MinimumLevel = LogLevel.Information;
            loggerFactory.AddConsole();

            // Configure the HTTP request pipeline.

            // Add the following to the request pipeline only in development environment.
            if (env.IsDevelopment())
            {
                app.UseErrorPage();
            }
            else
            {
                // Add Error handling middleware which catches all application specific errors and
                // send the request to the following path or controller action.
                app.UseErrorHandler("/Error");
            }

            // Add static files to the request pipeline.
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

            // Add MVC to the request pipeline.
            app.UseMvc();
        }
    }
}
