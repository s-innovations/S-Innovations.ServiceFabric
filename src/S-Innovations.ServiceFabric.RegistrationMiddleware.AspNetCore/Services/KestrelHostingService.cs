using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Practices.Unity;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using SInnovations.ServiceFabric.Gateway.Actors;
using SInnovations.ServiceFabric.Unity;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Services
{
    public class GatewayOptions
    {
        public string ServerName { get; set; }

    }
    public class KestrelHostingServiceOptions
    {
        public string ReverseProxyLocation { get; set; }
        public string ServiceEndpointName { get; set; }

        public GatewayOptions GatewayOptions { get; set; } = new GatewayOptions();
    }

    public static class KestrelHostingExtensions
    {
        public static IUnityContainer WithKestrelHosting<TStartup>(this IUnityContainer container, string serviceType, KestrelHostingServiceOptions options)
            where TStartup : class
        {
            return container.WithKestrelHosting<KestrelHostingService<TStartup>, TStartup>(serviceType, options);
        }

        public static IUnityContainer WithKestrelHosting<THostingService, TStartup>(this IUnityContainer container, string serviceType, KestrelHostingServiceOptions options)
          where THostingService : KestrelHostingService<TStartup>
          where TStartup : class
        {
            container.RegisterInstance(options);
            container.WithStatelessService<THostingService>(serviceType);
            return container;
        }
    }
    /// <summary>
    /// A specialized stateless service for hosting ASP.NET Core web apps.
    /// </summary>
    public class KestrelHostingService<TStartUp> : StatelessService where TStartUp : class
    {
        protected KestrelHostingServiceOptions Options { get; set; }
        public KestrelHostingService(KestrelHostingServiceOptions options, StatelessServiceContext serviceContext)
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
                      var context =serviceContext.CodePackageActivationContext;
                        // ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting WebListener on {url}");
                        var config = context.GetConfigurationPackageObject("Config");

                        var builder=new WebHostBuilder().UseKestrel()
                                    .ConfigureServices(this.ConfigureServices)
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<TStartUp>();

                        if(config.Settings.Sections.Contains("Environment"))
                        {
                            //http://stackoverflow.com/questions/39109666/asp-net-core-environment-variables-not-being-used-when-debugging-through-a-servi

                            var environments =config.Settings.Sections["Environment"];
                            if(environments.Parameters.Contains("ASPNETCORE_ENVIRONMENT"))
                            {
                                builder = builder.UseEnvironment(environments.Parameters["ASPNETCORE_ENVIRONMENT"].Value);

                            }

                        }
                        return builder
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


            await gateway.OnHostOpenAsync(new GatewayEventData
            {
                ServerName = Options.GatewayOptions.ServerName,
                ReverseProxyLocation = Options.ReverseProxyLocation ?? "/",
                IPAddressOrFQDN = FabricRuntime.GetNodeContext().IPAddressOrFQDN,
                BackendPath = $"{endpoint.Protocol.ToString().ToLower()}://{FabricRuntime.GetNodeContext().IPAddressOrFQDN}:{endpoint.Port}"
            }); //(Options.ReverseProxyPath ?? "/", $"{endpoint.Protocol})://{FabricRuntime.GetNodeContext().IPAddressOrFQDN}:{endpoint.Port}");


        }
    }
}
