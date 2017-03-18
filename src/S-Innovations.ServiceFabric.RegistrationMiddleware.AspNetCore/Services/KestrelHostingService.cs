using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Practices.Unity;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using SInnovations.ServiceFabric.Gateway.Actors;
using SInnovations.ServiceFabric.Gateway.Model;
using SInnovations.ServiceFabric.Unity;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceFabric.Services.Client;
using Serilog;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.AspNetCore.HttpOverrides;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Startup;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Services
{

    public class GatewayOptions
    {
        public string Key { get; set; }
        public string ReverseProxyLocation { get; set; }
        public string ServerName { get; set; }
        public SslOptions Ssl { get; set; } = new SslOptions();
    }
    public class KestrelHostingServiceOptions
    {
        //  public string ReverseProxyLocation { get; set; }
        public string ServiceEndpointName { get; set; }

        public GatewayOptions GatewayOptions { get; set; } = new GatewayOptions();
    }

    public class ApplicationInsights
    {
        public string InstrumentationKey { get; set; }

    }

    public static class KestrelHostingExtensions
    {
        public static IUnityContainer ConfigureSerilogging(this IUnityContainer container, Action<LoggerConfiguration> configure)
        {
            if (!container.IsRegistered<LoggerConfiguration>())
            {

                container.RegisterInstance(new LoggerConfiguration());
                container.RegisterType<ILoggerFactory>(new ContainerControlledLifetimeManager(),
                     new InjectionFactory((c) => new LoggerFactory().AddSerilog(c.Resolve<LoggerConfiguration>().CreateLogger())));
            }

            configure(container.Resolve<LoggerConfiguration>());

            return container;
        }
        public static IUnityContainer ConfigureApplicationInsights(this IUnityContainer container)
        {


            container.Configure<ApplicationInsights>(container.Resolve<IConfiguration>().GetSection("ApplicationInsights"));

            container.ConfigureSerilogging((logConfiguration) =>
            {

                logConfiguration.WriteTo.ApplicationInsightsTraces(container.Resolve<ApplicationInsights>().InstrumentationKey, Serilog.Events.LogEventLevel.Information);
            });



            return container;
        }

        public static IUnityContainer WithServiceProxy<TServiceInterface>(this IUnityContainer container, string serviceName, string listenerName = null)
            where TServiceInterface : IService
        {
            return container.RegisterType<TServiceInterface>(new HierarchicalLifetimeManager(),
                      new InjectionFactory(c => ServiceProxy.Create<TServiceInterface>(
                          new Uri(serviceName), listenerName: listenerName)));

        }
        public static IUnityContainer WithKestrelHosting<TStartup>(this IUnityContainer container, string serviceType, KestrelHostingServiceOptions options)
            where TStartup : class
        {
            return container.WithKestrelHosting<KestrelHostingService<TStartup>, TStartup>(serviceType, options);
        }

        public static IUnityContainer WithKestrelHosting<THostingService, TStartup>(this IUnityContainer container, string serviceType, KestrelHostingServiceOptions options)
          where THostingService : KestrelHostingService<TStartup>
          where TStartup : class
        {

            container.WithStatelessService<THostingService>(serviceType, child => { child.RegisterInstance(options); });
            return container;
        }

        public static IUnityContainer WithKestrelHosting(this IUnityContainer container, string serviceType, KestrelHostingServiceOptions options, Action<IWebHostBuilder> builder)
        {
            container.WithStatelessService<KestrelHostingService>(serviceType, child =>
            {
                child.RegisterInstance(options);
                child.RegisterType<KestrelHostingService>(new InjectionProperty("WebBuilderConfiguration", builder));
            });

            return container;
        }
    }

    public interface IUnityWrapper
    {
        IUnityContainer ScopedContainer
        {
            get;
        }
    }
    public class ScopeWrap
    {

    }
    public class UnityWrapper : IUnityWrapper, IDisposable
    {
        public UnityWrapper(IUnityContainer Parent)
        {
            ScopedContainer = Parent.CreateChildContainer();
        }
        public IUnityContainer ScopedContainer { get; set; }

        public void Dispose()
        {
            ScopedContainer.Dispose();
        }
    }

    //public class UnityServiceProviderFactory : IServiceProviderFactory<IServiceCollection>{
    //    public IServiceCollection CreateBuilder(IServiceCollection services)
    //    {
    //        return services;
    //    }

    //    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
    //    {
    //        return containerBuilder.GetServiceFabricServiceProvider();
    //    }
    //}


    public class CustomKestrelCommunicationListener : KestrelCommunicationListener
    {
        private readonly ServiceContext _serviceContext;
        public CustomKestrelCommunicationListener(ServiceContext serviceContext, string serviceEdpoint, Func<string, IWebHost> build) : base(serviceContext,serviceEdpoint, build)
        {
            _serviceContext = serviceContext;
        }

        public override async Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            var url = await base.OpenAsync(cancellationToken).ConfigureAwait(false);
            
            return url.Replace("[::]", this._serviceContext.NodeContext.IPAddressOrFQDN);
        }
    }


    public class KestrelHostingService : StatelessService
    {
        public Action<IWebHostBuilder> WebBuilderConfiguration { get; set; }

        protected KestrelHostingServiceOptions Options { get; set; }
        protected IUnityContainer Container { get; set; }

        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        public KestrelHostingService(
            KestrelHostingServiceOptions options,
            StatelessServiceContext serviceContext,
            ILoggerFactory factory,
            IUnityContainer container)
            : base(serviceContext)
        {
            Options = options;
            Container = container;
            _logger = factory.CreateLogger<KestrelHostingService>();

            _logger.LogInformation("Creating " + nameof(KestrelHostingService) + " for {@options}", Options);
        }

        protected virtual void ConfigureServices(IServiceCollection services)
        {
            _logger.LogInformation("ConfigureServices of {gatewayKey}", Options.GatewayOptions.Key);

            services.AddSingleton(this.Context);
            services.AddSingleton<ServiceContext>(this.Context);

            services.AddSingleton(Container);
            services.AddSingleton<IServiceProviderFactory<IServiceCollection>>(new UnityServiceProviderFactory(Container));
            services.AddTransient<IStartupFilter, UseForwardedHeadersStartupFilter>();
            //services.AddSingleton(new UnityWrapper(this.Container));

            //   services.AddScoped<IUnityContainer>(p => p.GetService<IUnityWrapper>()?.ScopedContainer ?? p.GetService<UnityWrapper>().ScopedContainer);
            // services.AddScoped<IUnityWrapper>((p)=>new UnityWrapper(p.GetService<IUnityContainer>()));  
            //    services.AddScoped<IUnityContainer>(p => p.GetService<ScopeWrapper>().ScopedContainer);

            //foreach (var registration in Container.Registrations)
            //{
            //    if (registration.RegisteredType == typeof(IEnumerable<>))
            //    {

            //    }
            //    else if (registration.MappedToType == registration.RegisteredType)
            //    {

            //        services.AddSingleton(Container.Resolve(registration.RegisteredType));
            //    }
            //    else if (registration.LifetimeManagerType == typeof(ContainerControlledLifetimeManager))
            //    {
            //        services.AddSingleton(Container.Resolve(registration.RegisteredType));
            //    }
            //    else
            //    {
            //        services.Add(new ServiceDescriptor(registration.RegisteredType, registration.MappedToType, GetLifeTime(registration)));
            //    }
            //}
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

                    new CustomKestrelCommunicationListener(serviceContext, Options.ServiceEndpointName, url =>
                    {
                        try {

                            _logger.LogInformation("building kestrel app for {url} in {gatewayKey}",url,Options.GatewayOptions.Key);

                            var context =serviceContext.CodePackageActivationContext;
                            var config = context.GetConfigurationPackageObject("Config");

                            var builder=new WebHostBuilder().UseKestrel()
                                        .ConfigureServices(ConfigureServices)
                                        .UseContentRoot(Directory.GetCurrentDirectory());

                            if (Container.IsRegistered<IConfiguration>())
                            {
                                 _logger.LogInformation("UseConfiguration for {gatewayKey}", Options.GatewayOptions.Key);
                                builder.UseConfiguration(Container.Resolve<IConfiguration>());
                            }


                            if(config.Settings.Sections.Contains("Environment"))
                            {
                                //http://stackoverflow.com/questions/39109666/asp-net-core-environment-variables-not-being-used-when-debugging-through-a-servi

                                

                                var environments =config.Settings.Sections["Environment"];
                                if(environments.Parameters.Contains("ASPNETCORE_ENVIRONMENT"))
                                {
                                    var environment = environments.Parameters["ASPNETCORE_ENVIRONMENT"].Value;
                                    _logger.LogInformation("UseEnvironment {environment} for {gatewayKey}",environment, Options.GatewayOptions.Key);
                                    builder = builder.UseEnvironment(environment);

                                }

                            }

                            if (Container.IsRegistered<ILoggerFactory>())
                            {
                                _logger.LogInformation("UseLoggerFactory for {gatewayKey}", Options.GatewayOptions.Key);
                                builder.UseLoggerFactory(Container.Resolve<ILoggerFactory>());
                            }


                            ConfigureBuilder(builder);

                            return builder.UseUrls(url).Build();

                            }catch(Exception ex)
                            {
                                _logger.LogWarning(new EventId(),ex,"failed to build app pipeline");
                                throw;
                            }
                    }),"kestrel")
            };
        }

        public virtual void ConfigureBuilder(IWebHostBuilder builder)
        {
            WebBuilderConfiguration?.Invoke(builder);
        }

        protected override async Task OnOpenAsync(CancellationToken cancellationToken)
        {
            try
            {
                var gateway = ActorProxy.Create<IGatewayServiceManagerActor>(new ActorId(0), "S-Innovations.ServiceFabric.GatewayApplication", "GatewayServiceManagerActorService");


                if (!this.GetAddresses().TryGetValue("kestrel", out string backAddress))
                {

                }

                if (!string.IsNullOrEmpty(Options.ServiceEndpointName))
                {
                    var endpoint = Context.CodePackageActivationContext.GetEndpoint(Options.ServiceEndpointName);
                    backAddress = $"{endpoint.Protocol.ToString().ToLower()}://{Context.NodeContext.IPAddressOrFQDN}:{endpoint.Port}";
                }


                // var resolver = ServicePartitionResolver.GetDefault();
                //  var fabricClient = new FabricClient();
                //  var servicse = fabricClient.QueryManager.GetPartitionListAsync()
                // var a = this.GetAddresses();
                //  Console.WriteLine(string.Join(",", a.Select(k => k.Key + k.Value)));
                await base.OnOpenAsync(cancellationToken);


                await gateway.RegisterGatewayServiceAsync(new GatewayServiceRegistrationData
                {
                    Key = $"{Options.GatewayOptions.Key ?? Context.CodePackageActivationContext.GetServiceManifestName()}-{Context.NodeContext.IPAddressOrFQDN}",
                    IPAddressOrFQDN = Context.NodeContext.IPAddressOrFQDN,
                    ServerName = Options.GatewayOptions.ServerName,
                    ReverseProxyLocation = Options.GatewayOptions.ReverseProxyLocation ?? "/",
                    Ssl = Options.GatewayOptions.Ssl,
                    BackendPath = backAddress,
                    ServiceName = Context.ServiceName,
                    ServiceVersion = Context.CodePackageActivationContext.GetServiceManifestVersion()
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(new EventId(), ex, "OnOpenAsync failed");
                throw;
            }

        }
    }
    /// <summary>
    /// A specialized stateless service for hosting ASP.NET Core web apps.
    /// </summary>
    public class KestrelHostingService<TStartUp> : KestrelHostingService where TStartUp : class
    {

        public KestrelHostingService(
            KestrelHostingServiceOptions options,
            StatelessServiceContext serviceContext,
            ILoggerFactory factory,
            IUnityContainer container)
            : base(options, serviceContext, factory, container)
        {

        }

        public override void ConfigureBuilder(IWebHostBuilder builder)
        {
            builder.UseStartup<TStartUp>();
        }
    }
}
