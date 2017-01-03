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

namespace SInnovations.ServiceFabric.GatewayService.Configuration
{
    public class FileCache : TokenCache
    {
        private readonly ILogger Logger;
        private readonly IDataProtector Protector;
        public string CacheFilePath;
        private static readonly object FileLock = new object();


        public FileCache(
           ILoggerFactory loggerFactory,
           IDataProtectionProvider dataProtectionProvider
            ) : this(loggerFactory, dataProtectionProvider, @".\TokenCache.dat")
        {

        }
        // Initializes the cache against a local file.
        // If the file is already present, it loads its content in the ADAL cache
        public FileCache(
            ILoggerFactory loggerFactory,
            IDataProtectionProvider dataProtectionProvider,
            string filePath)
        {
            CacheFilePath = filePath;
            Logger = loggerFactory.CreateLogger<FileCache>();
            Protector = dataProtectionProvider.CreateProtector(typeof(FileCache).FullName);

            this.AfterAccess = AfterAccessNotification;
            this.BeforeAccess = BeforeAccessNotification;
            lock (FileLock)
            {
                this.Deserialize(File.Exists(CacheFilePath) ?
                    Protector.Unprotect(File.ReadAllBytes(CacheFilePath))
                    : null);
            }
        }

        // Empties the persistent store.
        public override void Clear()
        {
            base.Clear();
            File.Delete(CacheFilePath);
        }

        // Triggered right before ADAL needs to access the cache.
        // Reload the cache from the persistent store in case it changed since the last access.
        void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (FileLock)
            {
                this.Deserialize(File.Exists(CacheFilePath) ?
                    Protector.Unprotect(File.ReadAllBytes(CacheFilePath))
                    : null);
            }
        }

        // Triggered right after ADAL accessed the cache.
        void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (this.HasStateChanged)
            {
                lock (FileLock)
                {
                    // reflect changes in the persistent store
                    File.WriteAllBytes(CacheFilePath,
                        Protector.Protect(this.Serialize()));
                    // once the write operation took place, restore the HasStateChanged bit to false
                    this.HasStateChanged = false;
                }
            }
        }
    }
    public class AzureADConfiguration
    {
        private readonly TokenCache _cache;
        public AzureADConfiguration(ConfigurationPackage configurationPackage, TokenCache cache)
        {
            _cache = cache;


            var section = configurationPackage.Settings.Sections["AzureResourceManager"].Parameters;
            AzureADServiceCredentials = ParseSecureString(section["AzureADServicePrincipal"].DecryptValue());
            TenantId = section["TenantId"].Value;

        }


        static ClientCredential ParseSecureString(SecureString value)
        {
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(value);
                var secureStringPassword = new SecureString();

                var chars = new char[1];
                var clientId = new StringBuilder();
                var clientIdDone = false;
                for (int i = 0; i < value.Length; i++)
                {
                    short unicodeChar = Marshal.ReadInt16(valuePtr, i * 2);
                    var c = Convert.ToChar(unicodeChar);


                    if (!clientIdDone)
                    {
                        if (c != ':')
                        {
                            clientId.Append(c);
                        }
                        else
                        {
                            clientIdDone = true;
                        }
                    }
                    else if (c != '\0')
                    {
                        secureStringPassword.AppendChar(c);

                    }

                    // handle unicodeChar
                }
                return new ClientCredential(clientId.ToString(), new SecureClientSecret(secureStringPassword));

            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        public string TenantId { get; set; }
        public ClientCredential AzureADServiceCredentials { get; set; }


        public async Task<string> GetAccessToken()
        {


            var ctx = new AuthenticationContext($"https://login.microsoftonline.com/{TenantId}", _cache);

            var token = await ctx.AcquireTokenAsync("https://management.azure.com/", AzureADServiceCredentials);

            return token.AccessToken;
        }


    }


    public class ArmError
    {

        public string Message { get; set; }

        public string Code { get; set; }
    }


    public class ArmErrorBase
    {

        public ArmError Error { get; set; }
    }

    public static class HttpClientExtensions
    {


        public static async Task<T> As<T>(this Task<HttpResponseMessage> messageTask)
        {
            var message = await messageTask;

            if (!typeof(ArmErrorBase).IsAssignableFrom(typeof(T)) && !message.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await message.Content.ReadAsStringAsync());
            }

            using (var stream = await message.Content.ReadAsStreamAsync())
            {
                using (var sr = new JsonTextReader(new StreamReader(stream)))
                {
                    var serializer = JsonSerializer.Create(new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                    });

                    return serializer.Deserialize<T>(sr);
                }
            }
        }
    }

    public class ArmClient
    {


        protected HttpClient Client { get; set; }
        public ArmClient(AuthenticationHeaderValue authorization)
        {
            Client = new HttpClient();
            Client.DefaultRequestHeaders.Authorization = authorization;
        }

        public ArmClient(string accessToken) : this(new AuthenticationHeaderValue("bearer", accessToken))
        {

        }

        public Task<T> ListKeysAsync<T>(string resourceId, string apiVersion)
        {
            var resourceUrl = $"https://management.azure.com/{resourceId.Trim('/')}/listkeys?api-version={apiVersion}";

            return Client.PostAsync(resourceUrl, new StringContent(string.Empty))
                .As<T>();
        }

        public Task<T> PatchAsync<T>(string resourceId, T value, string apiVersion)
        {
            var resourceUrl = $"https://management.azure.com/{resourceId.Trim('/')}?api-version={apiVersion}";
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), resourceUrl);
            var valuestr = JsonConvert.SerializeObject(value);
            request.Content = new StringContent(valuestr, Encoding.UTF8, "application/json");

            return Client.SendAsync(request)
                .As<T>();
        }

        public Task<T> GetAsync<T>(string resourceId, string apiVersion)
        {
            var resourceUrl = $"https://management.azure.com/{resourceId.Trim('/')}?api-version={apiVersion}";
            return Client.GetAsync(resourceUrl).As<T>();
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
  
    public class StorageConfiguration
    {
        private readonly ILogger Logger;
        private readonly AzureADConfiguration AzureAD;
        private readonly string StorageAccountId;
        public StorageConfiguration(
            ConfigurationPackage configurationPackage,
            AzureADConfiguration AzureAd,
            ILoggerFactory logFactory)
        {
            this.Logger = logFactory.CreateLogger<StorageConfiguration>();
            this.AzureAD = AzureAd;

            var section = configurationPackage.Settings.Sections["AzureResourceManager"].Parameters;
            StorageAccountId = section["ApplicationStorageAccountId"].Value;

        }

        public async Task<CloudStorageAccount> GetApplicationStorageAccountAsync()
        {


            var client = new ArmClient(await AzureAD.GetAccessToken());
            var keys = await client.ListKeysAsync<JObject>(StorageAccountId, "2016-01-01");

            var account = new CloudStorageAccount(new StorageCredentials(StorageAccountId.Substring(StorageAccountId.LastIndexOf("/") + 1), keys.SelectToken("keys[0].value").ToString()), true);

            return account;
        }


    }



    public static class UnityExtensions
    {
        public static IUnityContainer AddSingleton<T>(this IUnityContainer container)
        {
            return container.RegisterType<T>(new ContainerControlledLifetimeManager());
        }

        public static IUnityContainer AddScoped<TFrom, TTo>(this IUnityContainer container) where TTo : TFrom
        {
            return container.RegisterType<TFrom, TTo>(new HierarchicalLifetimeManager());
        }

        public static IUnityContainer AddScoped<TTo>(this IUnityContainer container, params InjectionMember[] injectionMembers)
        {
            return container.RegisterType<TTo>(new HierarchicalLifetimeManager(), injectionMembers);
        }

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
