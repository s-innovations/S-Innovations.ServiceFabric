using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Practices.Unity;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SInnovations.DnsMadeEasy;
using SInnovations.LetsEncrypt;
using SInnovations.LetsEncrypt.Clients;
using SInnovations.LetsEncrypt.DnsMadeEasyManager;
using SInnovations.LetsEncrypt.Services;
using SInnovations.LetsEncrypt.Services.Defaults;
using SInnovations.LetsEncrypt.Stores;
using SInnovations.LetsEncrypt.Stores.Defaults;
using SInnovations.ServiceFabric.Storage.Configuration;
using SInnovations.ServiceFabric.Unity;

namespace SInnovations.ServiceFabric.GatewayService.Configuration
{
    
    


    


   
    public static class DsnExtensions
    {
        public static IUnityContainer WithLetsEncryptService(this IUnityContainer container, LetsEncryptServiceOptions options)
        {
            container.RegisterInstance(options);
            container.AddScoped<IRS256SignerStore, InMemoryRS256SignerStore>();
            container.AddScoped<IRS256SignerService, DefaultRS256SignerService>();
            container.AddScoped<IAcmeClientService, DefaultAcmeClientService>();
            container.AddScoped<IAcmeRegistrationStore, InMemoryAcmeRegistrationStore>();
            container.AddScoped<ILetsEncryptChallengeService, DefaultDnsChallengeService>();
            container.AddScoped<IDnsClient, LetsEncryptDnsMadeEasyManager>();
            container.AddScoped<DnsMadeEasyClientCredetials, DnsMadeEasyOptions>();
            container.AddScoped<LetsEncryptService>();

            return container;
        }
    }
    

    
    public class DnsMadeEasyOptions : DnsMadeEasyClientCredetials
    {
        public DnsMadeEasyOptions(IOptions<KeyVaultOptions> keyvault)
        {
            var parts = keyvault.Value.DnsMadeEasyCredentials.Split(':');
            this.ApiKey = parts[0];
            this.ApiSecret = parts[1];
        }
    }
   
    public class KeyVaultOptions
    {
        public string DnsMadeEasyCredentials { get; set; }
    }

    public class KeyVaultSecretManager : IKeyVaultSecretManager
    {
        private readonly ILogger Logger;
        private readonly AzureADConfiguration AzureAD;
        public string KeyVaultUrl { get; set; }
        public KeyVaultClient Client { get; set; }

        public KeyVaultSecretManager(
          ConfigurationPackage configurationPackage,
          AzureADConfiguration AzureAd,
          ILoggerFactory logFactory)
        {
            this.Logger = logFactory.CreateLogger<StorageConfiguration>();
            this.AzureAD = AzureAd;

            var section = configurationPackage.Settings.Sections["AzureResourceManager"].Parameters;
            KeyVaultUrl = section["Azure.KeyVault.Uri"].Value;

            KeyVaultClient.AuthenticationCallback callback =
                (authority, resource, scope) => GetTokenFromClientSecret(authority, resource);

            Client = new KeyVaultClient(callback);
        }

        private async Task<string> GetTokenFromClientSecret(string authority, string resource)
        {
            var authContext = new AuthenticationContext(authority);
            var result = await authContext.AcquireTokenAsync(resource, AzureAD.AzureADServiceCredentials);
            return result.AccessToken;
        }
        /// <inheritdoc />
        public virtual string GetKey(SecretBundle secret)
        {
         
            return "KeyVault:"+ secret.SecretIdentifier.Name.Replace("--", ConfigurationPath.KeyDelimiter);
        }

        /// <inheritdoc />
        public virtual bool Load(SecretItem secret)
        {
          
            return true;
        }

    }
  
    



    

   
}
