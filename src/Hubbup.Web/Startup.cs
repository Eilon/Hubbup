﻿using System.IO;
using System.Net;
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

            // Increase default outgoing connection limit to a larger number to allow
            // more parallel requests to go out to GitHub.
            ServicePointManager.DefaultConnectionLimit = 10;
        }

        public IConfiguration Configuration { get; set; }

        // This method gets called by the runtime.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IRepoSetProvider>(new StaticRepoSetProvider());
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

        // Configure is called after ConfigureServices is called.
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

        // Entry point for the application.public static void Main(string[] args)
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
