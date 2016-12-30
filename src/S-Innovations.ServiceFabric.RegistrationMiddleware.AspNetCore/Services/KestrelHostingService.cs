using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using SInnovations.ServiceFabric.Gateway.Actors;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Services
{

    public class KestrelHostingServiceOptions{
        public string ReverseProxyPath { get;  set; }
        public string ServiceEndpointName { get; set; }
    }

    
    /// <summary>
    /// A specialized stateless service for hosting ASP.NET Core web apps.
    /// </summary>
    public class KestrelHostingService<TStartUp> : StatelessService where TStartUp : class
    {
        protected KestrelHostingServiceOptions Options { get; set; }
        public KestrelHostingService(KestrelHostingServiceOptions options , StatelessServiceContext serviceContext)
            : base(serviceContext)
        {
            Options = options;
        }

        protected virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(this.Context);
            services.AddSingleton<ServiceContext>(this.Context);
        }




        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new ServiceInstanceListener[]
            {
                new ServiceInstanceListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, Options.ServiceEndpointName, url =>
                    {
                       // ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting WebListener on {url}");
                       

                        return new WebHostBuilder().UseKestrel()
                                    .ConfigureServices(this.ConfigureServices)
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<TStartUp>()
                                    .UseUrls(url)
                                    .Build();
                    }))
            };
        }

        protected override async Task OnOpenAsync(CancellationToken cancellationToken)
        {
            var gateway = ActorProxy.Create<IGatewayServiceManagerActor>(new ActorId(0), "S-Innovations.ServiceFabric.GatewayApplication", "GatewayServiceManagerActorService");
            var endpoint = FabricRuntime.GetActivationContext().GetEndpoint(Options.ServiceEndpointName);
         
            await base.OnOpenAsync(cancellationToken);
            try
            {

                await gateway.OnHostOpenAsync(new GatewayEventData { ForwardPath = Options.ReverseProxyPath ?? "/", BackendPath = $"{endpoint.Protocol.ToString().ToLower()}://{FabricRuntime.GetNodeContext().IPAddressOrFQDN}:{endpoint.Port}" }); //(Options.ReverseProxyPath ?? "/", $"{endpoint.Protocol})://{FabricRuntime.GetNodeContext().IPAddressOrFQDN}:{endpoint.Port}");

            }
            catch (Exception ex)
            {

            }
         }
    }
}
