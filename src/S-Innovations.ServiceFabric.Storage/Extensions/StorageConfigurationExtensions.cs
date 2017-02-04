using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Practices.Unity;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using SInnovations.ServiceFabric.Storage.Configuration;
using SInnovations.ServiceFabric.Storage.Services;
using SInnovations.ServiceFabric.Unity;

namespace SInnovations.ServiceFabric.Storage.Extensions
{
    public static class StorageConfigurationExtensions
    {

        public static IUnityContainer ConfigureApplicationStorage(this IUnityContainer container)
        {

          

            return container
                .RegisterType<TokenCache, FileCache>(new ContainerControlledLifetimeManager(), new InjectionConstructor(typeof(ILoggerFactory), typeof(IDataProtectionProvider)))
                .RegisterType<IDataProtectionProvider>(new ContainerControlledLifetimeManager(), new InjectionFactory(c => DataProtectionProvider.Create(c.Resolve<ICodePackageActivationContext>().ApplicationName)))
                .AddSingleton<StorageConfiguration>();


            
        }
    }
}
