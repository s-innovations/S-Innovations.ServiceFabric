using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json.Linq;
using SInnovations.ServiceFabric.Storage.Clients;

namespace SInnovations.ServiceFabric.Storage.Configuration
{
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
}
