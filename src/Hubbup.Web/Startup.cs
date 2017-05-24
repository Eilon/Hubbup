using System.IO;
using System.Reflection;
using Hubbup.Web.DataSources;
using Hubbup.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            });

            services.AddOAuthAuthentication("GitHub", options =>
            {
                options.CallbackPath = new PathString("/signin-github");
                options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                options.UserInformationEndpoint = "https://api.github.com/user";
                options.ClaimsIssuer = "GitHub";

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

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory, IDataSource dataSource)
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
