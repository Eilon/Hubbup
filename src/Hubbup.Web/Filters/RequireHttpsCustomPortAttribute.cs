using System;
using System.Globalization;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.Filters;

namespace Hubbup.Web.Filters
{
    public class RequireHttpsCustomPortAttribute : RequireHttpsAttribute
    {
        public RequireHttpsCustomPortAttribute(int customSslPort)
        {
            CustomSslPort = customSslPort;
        }

        public RequireHttpsCustomPortAttribute(int customSslPort, string environmentName)
            : this(customSslPort)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public int CustomSslPort { get; }

        protected override void HandleNonHttpsRequest(AuthorizationContext filterContext)
        {
            var appliesToCurrentRequest = true;
            if (!string.IsNullOrEmpty(EnvironmentName))
            {
                var hostEnv = (IHostingEnvironment)filterContext.HttpContext.ApplicationServices.GetService(typeof(IHostingEnvironment));
                appliesToCurrentRequest = hostEnv.IsEnvironment(EnvironmentName);
            }

            if (appliesToCurrentRequest)
            {
                // Code is pretty much copied from https://github.com/aspnet/Mvc/blob/43226fe54de140db30746336f3d46130be47c0e4/src/Microsoft.AspNet.Mvc.Core/RequireHttpsAttribute.cs
                // ... but the port stuff is changed to use the custom port

                // only redirect for GET requests, otherwise the browser might not propagate the verb and request
                // body correctly.
                if (!string.Equals(filterContext.HttpContext.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    filterContext.Result = new HttpStatusCodeResult(StatusCodes.Status403Forbidden);
                }
                else
                {
                    var request = filterContext.HttpContext.Request;
                    var newUrl = string.Concat(
                        "https://",
                        ChangeHostPort(request.Host.ToUriComponent(), CustomSslPort),
                        request.PathBase.ToUriComponent(),
                        request.Path.ToUriComponent(),
                        request.QueryString.ToUriComponent());

                    // redirect to HTTPS version of page
                    filterContext.Result = new RedirectResult(newUrl, permanent: true);
                }
            }
            else
            {
                base.HandleNonHttpsRequest(filterContext);
            }
        }

        private static string ChangeHostPort(string hostAndPort, int newPort)
        {
            int indexOfColon = hostAndPort.IndexOf(':');
            if (indexOfColon == -1)
            {
                return hostAndPort + ":" + newPort.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                return hostAndPort.Substring(0, indexOfColon) + ":" + newPort.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
