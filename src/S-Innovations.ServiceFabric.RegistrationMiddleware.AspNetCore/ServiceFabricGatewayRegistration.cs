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
using Microsoft.Extensions.Primitives;

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
                    
#if OWIN
                    string[] values;
#else
                    StringValues values;
#endif
                    if (context.Request.Headers.TryGetValue("X-Forwarded-PathBase", out values))
                    {
                        context.Request.PathBase = new PathString(values.FirstOrDefault() + (context.Request.PathBase.HasValue ? context.Request.PathBase.Value : string.Empty));
                    }
                }

                if (context.Request.Headers.ContainsKey("X-Forwarded-Path"))
                {
#if OWIN
                    string[] path;
#else
                    StringValues path;
#endif
                    if (context.Request.Headers.TryGetValue("X-Forwarded-Path", out path))
                    {

                        var orignalPath = path.First(); // /hello/blog  /blog
                        var idx = orignalPath.IndexOf(context.Request.Path.Value, 1);
                        if (idx == -1)
                        {
                            idx = orignalPath.Length;
                        }

                        var pathBase = orignalPath.Substring(0, idx);
                        if (context.Request.PathBase.Value != pathBase)
                        {
                            context.Request.PathBase = new PathString( pathBase + (context.Request.PathBase.HasValue ? context.Request.PathBase.Value : string.Empty));
                        }
                    }
                }

                await Next.Invoke(context);
            }
        }
    }
}
