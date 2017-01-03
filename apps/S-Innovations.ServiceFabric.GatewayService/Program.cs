using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Practices.Unity;
using Serilog;
using SInnovations.LetsEncrypt;
using SInnovations.ServiceFabric.GatewayService.Actors;
using SInnovations.ServiceFabric.GatewayService.Configuration;
using SInnovations.ServiceFabric.GatewayService.Services;
using SInnovations.ServiceFabric.Unity;

namespace SInnovations.ServiceFabric.GatewayService
{




    public class Program
    {
        
        public static void Main(string[] args)
        {
            var log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .CreateLogger();

            
            using (var container = new UnityContainer().AsFabricContainer())
            {
                container.AddOptions();
                container.ConfigureLogging(new LoggerFactory().AddSerilog());

                container.ConfigureApplicationStorage();


                var keyvaultINfo = container.Resolve<KeyVaultSecretManager>();
                var configuration = new ConfigurationBuilder()
                    .AddAzureKeyVault(keyvaultINfo.KeyVaultUrl, keyvaultINfo.Client, keyvaultINfo)
                    .Build(container);        

                container.Configure<KeyVaultOptions>("KeyVault");

                container.WithLetsEncryptService(new LetsEncryptServiceOptions
                {
                    BaseUri = "https://acme-v01.api.letsencrypt.org"
                });

                container.WithStatelessService<NginxGatewayService>("GatewayServiceType");
                container.WithActor<GatewayServiceManagerActor>();


                var test = container.Resolve<StorageConfiguration>();
                var account = test.GetApplicationStorageAccountAsync().GetAwaiter().GetResult();
              
                Thread.Sleep(Timeout.Infinite);
            }
            
        }

       
    }
}
