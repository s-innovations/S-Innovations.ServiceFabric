using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Practices.Unity;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Serilog;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Model;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Services;
using SInnovations.ServiceFabric.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Extensions
{
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
}
