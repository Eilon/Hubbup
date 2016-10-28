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
        public IHostingEnvironment HostingEnvironment { get; }

        public Startup(IHostingEnvironment env)
        {
            HostingEnvironment = env;

            // Set up configuration sources
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddEnvironmentVariables();

            if (HostingEnvironment.IsDevelopment())
            {
                builder.AddUserSecrets();
            }

            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            //services.Configure<LocalJsonRepoSetProviderOptions>(options =>
            //{
            //    options.JsonFilePath = @"C:\GitHub\Hubbup-data\hubbup-data.json";
            //});
            //services.AddSingleton<IRepoSetProvider, LocalJsonRepoSetProvider>();

            //services.Configure<RemoteJsonRepoSetProviderOptions>(Configuration);

            services.Configure<RemoteJsonRepoSetProviderOptions>(options =>
            {
                options.JsonFileUrl = "https://raw.githubusercontent.com/Eilon/Hubbup-data/master/hubbup-data.json";
            });
            services.AddSingleton<IRepoSetProvider, RemoteJsonRepoSetProvider>();

            //services.AddSingleton<IRepoSetProvider>(new StaticRepoSetProvider());

            services.AddSingleton<IPersonSetProvider>(new StaticPersonSetProvider());

            services.AddMemoryCache();

            services.AddSession();

            services.AddAuthentication();

            services.Configure<SharedAuthenticationOptions>(options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
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
            loggerFactory.AddConsole(minLevel: LogLevel.Information);
            loggerFactory.AddDebug(minLevel: LogLevel.Trace);

            if (HostingEnvironment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseCookieAuthentication(new CookieAuthenticationOptions()
            {
                LoginPath = new PathString("/signin")
            });

            app.UseGitHubAuthentication(options =>
            {
                options.ClientId = Configuration["GitHubClientId"];
                options.ClientSecret = Configuration["GitHubClientSecret"];
                options.Scope.Add("repo");
                options.SaveTokens = true;
                options.AutomaticChallenge = true;
            });

            app.UseSession();

            app.UseMvc();
        }

        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseConfiguration(new ConfigurationBuilder().AddCommandLine(args).Build())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
