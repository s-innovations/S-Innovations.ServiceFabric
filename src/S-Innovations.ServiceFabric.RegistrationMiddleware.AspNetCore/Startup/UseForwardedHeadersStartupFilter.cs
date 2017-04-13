using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Startup
{
    public class UseForwardedHeadersStartupFilter : IStartupFilter
    {
        private const string XForwardedPathBase = "X-Forwarded-PathBase";
        private readonly string serviceFabricKey;
        public UseForwardedHeadersStartupFilter(string serviceFabricKey)
        {
            this.serviceFabricKey = serviceFabricKey;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> nextBuilder)
        {
            return builder =>
            {
                builder.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
                });

                builder.Use(async (context, next) =>
                {
                    if(context.Request.Headers.TryGetValue("X-ServiceFabric-Key", out StringValues serviceFabricKey))
                    {
                        if (!serviceFabricKey.FirstOrDefault().Equals(this.serviceFabricKey))
                        {
                            context.Response.StatusCode = StatusCodes.Status410Gone;
                            return;
                        }
                    }

                    if (context.Request.Headers.TryGetValue(XForwardedPathBase, out StringValues value))
                    {
                        context.Request.PathBase = new PathString(value);
                    }


                    await next();
                });

                nextBuilder(builder);
            };
        }

    }
}
