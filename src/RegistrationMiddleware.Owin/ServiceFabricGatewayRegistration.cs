using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using SInnovations.ServiceFabric.Gateway.Model;
using Newtonsoft.Json;
using SInnovations.ServiceFabric.Gateway;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.Owin
{



    public static class ServiceRegistrationMiddlewareExtensions
    {

        
        public static IAppBuilder UseAsServiceFabricGatewayService(this IAppBuilder app, GatewayServiceInformation gatewayInfo )
        {

            app.Use(typeof(ServiceRegistrationMiddleware), gatewayInfo);

            return app;

        }
    }
    public class ServiceRegistrationMiddleware : OwinMiddleware
    {
       
        private readonly GatewayServiceInformation _info;
        public ServiceRegistrationMiddleware(OwinMiddleware next, GatewayServiceInformation info) : base(next)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            _info = info; 
        }

        public override async Task Invoke(IOwinContext context)
        {

            if (context.Request.Path.StartsWithSegments(new PathString(Constants.GatewayMetadataPath)))
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
                await Next.Invoke(context);
            }
        }
    }
}
