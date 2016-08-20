using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SInnovations.ServiceFabric.Gateway;
using SInnovations.ServiceFabric.Gateway.Model;

#if OWIN
using Microsoft.Owin;
using Owin;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.Owin

#else
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore
#endif


{
   
    public class ServiceRegistrationMiddleware
#if OWIN
        : OwinMiddleware 
#endif
    {

        private readonly GatewayServiceInformation _info;

#if OWIN
     public ServiceRegistrationMiddleware(OwinMiddleware next, GatewayServiceInformation info) : base(next) {
#else
        private readonly RequestDelegate Next;      
        public ServiceRegistrationMiddleware(RequestDelegate next , GatewayServiceInformation info)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            Next = next;
#endif

            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

          
            _info = info;
        }

        
#if OWIN
         public override async Task Invoke(IOwinContext context)
#else
        public async Task Invoke(HttpContext context)
#endif
        {


#if OWIN
            if (context.Request.Path.StartsWithSegments(new PathString(Constants.GatewayMetadataPath)))
#else
            if (context.Request.Path.StartsWithSegments(Constants.GatewayMetadataPath))
#endif
            {
                await context.Response.WriteAsync(JsonConvert.SerializeObject(_info));
            }
            else
            {
                //Move "X-Forwarded-PathBase" header into PathBase for downstream middlewares to use. 
                
                if (context.Request.Headers.ContainsKey("X-Forwarded-PathBase"))
                {
                    var vlues = context.Request.Headers["X-Forwarded-PathBase"];
                    context.Request.PathBase = new PathString( vlues.FirstOrDefault() + (context.Request.PathBase.HasValue ? context.Request.PathBase.Value : string.Empty));

                }

                await Next.Invoke(context);
            }
        }
    }
}
