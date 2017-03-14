using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Primitives;
using System;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Startup
{
    public class UseForwardedHeadersStartupFilter : IStartupFilter
    {
        private const string XForwardedPathBase = "X-Forwarded-PathBase";

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
