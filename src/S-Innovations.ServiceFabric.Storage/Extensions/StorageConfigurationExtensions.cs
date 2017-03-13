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
using Microsoft.WindowsAzure.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.WindowsAzure.Storage.Auth;

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
        public static IServiceCollection AddApplicationStorageDataProtection(this IServiceCollection services, IUnityContainer container, X509Certificate2 cert )
        {
            if (container != null)
            {

                try
                {
                    var storage = container.Resolve<IApplicationStorageService>();
                    var token = storage.GetApplicationStorageSharedAccessSignature().GetAwaiter().GetResult();
                    var name = storage.GetApplicationStorageAccountNameAsync().GetAwaiter().GetResult();
                    var a = new CloudStorageAccount(new StorageCredentials(token), name, null, true);
                    var c = a.CreateCloudBlobClient().GetContainerReference("dataprotection");
                    c.CreateIfNotExists();

                    services.AddDataProtection()
                     .ProtectKeysWithCertificate(cert)
                     .PersistKeysToAzureBlobStorage(c.GetBlockBlobReference("dummy.csrf"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            return services;
        }
    }
}
