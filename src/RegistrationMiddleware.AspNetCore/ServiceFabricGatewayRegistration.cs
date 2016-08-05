using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using SInnovations.ServiceFabric.Gateway.Model;
using SInnovations.ServiceFabric.Gateway.Services;
using Microsoft.Extensions.DependencyInjection;
using SInnovations.ServiceFabric.Gateway;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore
{  
    public static class ServiceRegistrationMiddlewareExtensions
    {

        public static IServiceCollection AddServiceFabricGatewayService(this IServiceCollection services)
        {
            return services;
        }
        public static IApplicationBuilder UseAsServiceFabricGatewayService(this IApplicationBuilder app, GatewayServiceInformation gatewayInfo )
        {

            app.UseMiddleware<ServiceRegistrationMiddleware>(gatewayInfo);

            return app;

        }
    }
    public class ServiceRegistrationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly GatewayServiceInformation _info;
        public ServiceRegistrationMiddleware(RequestDelegate next , GatewayServiceInformation info)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            _next = next;
            _info = info;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments(Constants.GatewayMetadataPath))
            {
                await context.Response.WriteAsync(JsonConvert.SerializeObject(
                    new
                    {
                        PathPrefix = _info.PathPrefix
                    }
                    ));
            }
            else
            {
                await _next(context);
            }
        }
    }
}
