using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SInnovations.ServiceFabric.Gateway.Model;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore
{
    public static class ServiceRegistrationMiddlewareExtensions
    {

        public static IServiceCollection AddServiceFabricGatewayService(this IServiceCollection services)
        {
            return services;
        }
        public static IApplicationBuilder UseAsServiceFabricGatewayService(this IApplicationBuilder app, GatewayServiceInformation gatewayInfo)
        {

            app.UseMiddleware<ServiceRegistrationMiddleware>(gatewayInfo);

            return app;

        }
        public static IApplicationBuilder AsServiceFabricGatewayService(this IApplicationBuilder app)
        {

            app.UseMiddleware<ServiceRegistrationMiddleware>(new GatewayServiceInformation());

            return app;

        }

        


    }
}
