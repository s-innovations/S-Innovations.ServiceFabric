using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.WindowsAzure.Storage;
using SInnovations.LetsEncrypt;
using SInnovations.ServiceFabric.Gateway.Actors;
using SInnovations.ServiceFabric.Gateway.Model;
using SInnovations.ServiceFabric.GatewayService.Configuration;
using SInnovations.ServiceFabric.GatewayService.Model;
using SInnovations.ServiceFabric.Storage.Configuration;
using Microsoft.ServiceFabric.Services.Remoting;
using System.Threading;
using Microsoft.ServiceFabric.Actors.Query;
using Microsoft.ServiceFabric.Actors.Client;

namespace SInnovations.ServiceFabric.GatewayService.Actors
{

    public interface IManyfoldActorService : IService
    {
        Task<IDictionary<long, DateTimeOffset>> GetLastUpdatedAsync(CancellationToken cancellationToken);
        Task<CertGenerationState> GetCertGenerationInfoAsync(string hostname, SslOptions options, CancellationToken cancellationToken);

        Task<List<GatewayServiceRegistrationData>> GetGatewayServicesAsync(CancellationToken cancellationToken);
        Task DeleteGatewayServiceAsync(string key, CancellationToken cancellationToken);
    }

    public class ManyfoldActorService : ActorService, IManyfoldActorService
    {
        public ManyfoldActorService(
            StatefulServiceContext context,
            ActorTypeInformation actorTypeInfo,
            Func<ActorService, ActorId, ActorBase> actorFactory = null,
            Func<ActorBase, IActorStateProvider, IActorStateManager> stateManagerFactory = null,
            IActorStateProvider stateProvider = null, ActorServiceSettings settings = null)
            : base(context, actorTypeInfo, actorFactory, stateManagerFactory, stateProvider, settings)
        {

        }
        public async Task DeleteGatewayServiceAsync(string key, CancellationToken cancellationToken)
        {
            ContinuationToken continuationToken = null;

            do
            {

                var page = await this.StateProvider.GetActorsAsync(100, continuationToken, cancellationToken);

                foreach (var actor in page.Items)
                {
                    if (await this.StateProvider.ContainsStateAsync(actor, GatewayServiceManagerActor.STATE_PROXY_DATA_NAME, cancellationToken))
                    {
                        var registrations = await this.StateProvider.LoadStateAsync<List<GatewayServiceRegistrationData>>(actor, GatewayServiceManagerActor.STATE_PROXY_DATA_NAME, cancellationToken);

                        if (registrations.RemoveAll(registration => registration.Key == key) > 0)
                        {
                            var changes = new ActorStateChange(
                                    GatewayServiceManagerActor.STATE_PROXY_DATA_NAME,
                                    typeof(List<GatewayServiceRegistrationData>),
                                    registrations, StateChangeKind.Update);
                            var time = new ActorStateChange(
                                GatewayServiceManagerActor.STATE_LAST_UPDATED_NAME, typeof(DateTimeOffset), DateTimeOffset.UtcNow, StateChangeKind.Update);


                            await this.StateProvider.SaveStateAsync(actor, new[] { changes, time }, cancellationToken);


                        }
                    }
                }

                continuationToken = page.ContinuationToken;
            }
            while (continuationToken != null);

        }
        public async Task<List<GatewayServiceRegistrationData>> GetGatewayServicesAsync(CancellationToken cancellationToken)
        {
            ContinuationToken continuationToken = null;
            var all = new List<GatewayServiceRegistrationData>();

            do
            {

                var page = await this.StateProvider.GetActorsAsync(100, continuationToken, cancellationToken);

                foreach (var actor in page.Items)
                {
                    if (await this.StateProvider.ContainsStateAsync(actor, GatewayServiceManagerActor.STATE_PROXY_DATA_NAME, cancellationToken))
                    {
                        var count = await this.StateProvider.LoadStateAsync<List<GatewayServiceRegistrationData>>(actor, GatewayServiceManagerActor.STATE_PROXY_DATA_NAME, cancellationToken);
                        all.AddRange(count);
                    }
                }

                continuationToken = page.ContinuationToken;
            }
            while (continuationToken != null);

            return all;
        }

        public async Task<IDictionary<long, DateTimeOffset>> GetLastUpdatedAsync(CancellationToken cancellationToken)
        {
            ContinuationToken continuationToken = null;
            var actors = new Dictionary<long, DateTimeOffset>();

            do
            {

                var page = await this.StateProvider.GetActorsAsync(100, continuationToken, cancellationToken);

                foreach (var actor in page.Items)
                {
                    if (await this.StateProvider.ContainsStateAsync(actor, GatewayServiceManagerActor.STATE_LAST_UPDATED_NAME, cancellationToken))
                    {
                        var count = await this.StateProvider.LoadStateAsync<DateTimeOffset>(actor, GatewayServiceManagerActor.STATE_LAST_UPDATED_NAME, cancellationToken);
                        actors.Add(actor.GetLongId(), count);
                    }
                }

                continuationToken = page.ContinuationToken;
            }
            while (continuationToken != null);

            return actors;
        }

        public async Task<CertGenerationState> GetCertGenerationInfoAsync(string hostname, SslOptions options, CancellationToken cancellationToken)
        {
            ContinuationToken continuationToken = null;

            do
            {

                var page = await this.StateProvider.GetActorsAsync(100, continuationToken, cancellationToken);

                foreach (var actor in page.Items)
                {
                    if (await this.StateProvider.ContainsStateAsync(actor, $"cert_{hostname}", cancellationToken))
                        return await this.StateProvider.LoadStateAsync<CertGenerationState>(actor, $"cert_{hostname}", cancellationToken);

                }

                continuationToken = page.ContinuationToken;
            }
            while (continuationToken != null);

            return null;
        }


    }

    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Persisted)]
    [ActorService()]
    public class GatewayServiceManagerActor : Actor, IGatewayServiceManagerActor, IRemindable
    {
        private const string REMINDER_NAME = "processSslQueue";
        private const string CERT_QUEUE_NAME = "certQueue";
        public const string STATE_LAST_UPDATED_NAME = "lastUpdated";
        public const string STATE_PROXY_DATA_NAME = "proxyData";

        private readonly StorageConfiguration Storage;
        private readonly LetsEncryptService letsEncrypt;

        private CloudStorageAccount StorageAccount;

        public GatewayServiceManagerActor(
            ActorService actorService,
            ActorId actorId,
            StorageConfiguration storage,
            LetsEncryptService letsEncrypt)
            : base(actorService, actorId)
        {
            Storage = storage;
            this.letsEncrypt = letsEncrypt;
        }



        public Task<List<GatewayServiceRegistrationData>> GetGatewayServicesAsync() => StateManager.GetStateAsync<List<GatewayServiceRegistrationData>>("proxyData");

        //   public Task<DateTimeOffset> GetLastUpdatedAsync() => StateManager.GetOrAddStateAsync(STATE_LAST_UPDATED_NAME, DateTimeOffset.MinValue);

        public async Task RequestCertificateAsync(string hostname, SslOptions options)
        {
            if (await StateManager.ContainsStateAsync($"cert_{hostname}"))
                return;

            await StateManager.AddStateAsync($"cert_{hostname}", new CertGenerationState { HostName = hostname, SslOptions = options });

            await AddHostnameToQueue(hostname);

            await RegisterReminderAsync(REMINDER_NAME, new byte[0],
               TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(-1));

        }

        public async Task SetupStorageServiceAsync(int instanceCount)
        {
            var client = new FabricClient();
            var codeContext = this.ActorService.Context.CodePackageActivationContext;

            var applicationName = new Uri(codeContext.ApplicationName.StartsWith("fabric:/") ? codeContext.ApplicationName : $"fabric:/{codeContext.ApplicationName}");

            var services = await client.QueryManager.GetServiceListAsync(applicationName);

            if (!services.Any(s => s.ServiceTypeName == "ApplicationStorageServiceType"))
            {

                await client.ServiceManager.CreateServiceAsync(new StatelessServiceDescription
                {
                    ServiceTypeName = "ApplicationStorageServiceType",
                    ApplicationName = applicationName,
                    ServiceName = new Uri(applicationName.ToString() + "/ApplicationStorageService"),
                    InstanceCount = instanceCount,
                    PartitionSchemeDescription = new SingletonPartitionSchemeDescription()
                });

            }
        }

        public async Task ReceiveReminderAsync(string reminderName, byte[] context, TimeSpan dueTime, TimeSpan period)
        {
            if (reminderName.Equals(REMINDER_NAME))
            {

                var certs = StorageAccount.CreateCloudBlobClient().GetContainerReference("certs");
                await certs.CreateIfNotExistsAsync();



                var store = await StateManager.GetStateAsync<List<string>>(CERT_QUEUE_NAME).ConfigureAwait(false);
                var hostname = store.First();

                var certBlob = certs.GetBlockBlobReference($"{hostname}.crt");
                var keyBlob = certs.GetBlockBlobReference($"{hostname}.key");


                var certInfo = await StateManager.GetStateAsync<CertGenerationState>($"cert_{hostname}");


                if (!(await certBlob.ExistsAsync() && await keyBlob.ExistsAsync()))
                {



                    try
                    {

                        var cert = await letsEncrypt.GenerateCertPairAsync(new GenerateCertificateRequestOptions
                        {
                            DnsIdentifier = certInfo.HostName,
                            SignerEmail = certInfo.SslOptions.SignerEmail
                        });


                        await certBlob.UploadFromByteArrayAsync(cert.Item2, 0, cert.Item2.Length);
                        await keyBlob.UploadFromByteArrayAsync(cert.Item1, 0, cert.Item1.Length);

                        certInfo.Completed = true;
                    }
                    catch (Exception ex)
                    {
                        await certs.GetBlockBlobReference($"{hostname}.err").UploadTextAsync(ex.ToString());

                    }




                }
                else
                {
                    certInfo.Completed = true;
                }

                await StateManager.SetStateAsync($"cert_{hostname}", certInfo);

                var missing = store.Skip(1).ToList();
                await StateManager.SetStateAsync(CERT_QUEUE_NAME, missing);
                if (missing.Any())
                {
                    await RegisterReminderAsync(
                      REMINDER_NAME, new byte[0],
                      TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(-1));
                }

                await StateManager.SetStateAsync(STATE_LAST_UPDATED_NAME, DateTimeOffset.UtcNow);
            }

        }

        public async Task RegisterGatewayServiceAsync(GatewayServiceRegistrationData data)
        {

            var proxies = await GetGatewayServicesAsync();

            var found = proxies.FirstOrDefault(i => i.Key == data.Key);
            if (found == null)
            {
                proxies.Add(data);
            }
            else
            {
                proxies[proxies.IndexOf(found)] = data;
            }

            await StateManager.SetStateAsync(STATE_PROXY_DATA_NAME, proxies);
            await StateManager.SetStateAsync(STATE_LAST_UPDATED_NAME, DateTimeOffset.UtcNow);


        }

        private async Task AddHostnameToQueue(string hostname)
        {
            var store = await StateManager.GetOrAddStateAsync(CERT_QUEUE_NAME, new List<string> { }).ConfigureAwait(false);
            store.Add(hostname);
            await StateManager.SetStateAsync(CERT_QUEUE_NAME, store);
        }


        protected override async Task OnActivateAsync()
        {

            StorageAccount = await Storage.GetApplicationStorageAccountAsync();

            await this.StateManager.TryAddStateAsync(STATE_PROXY_DATA_NAME, new List<GatewayServiceRegistrationData>());

            await base.OnActivateAsync();
        }

        public async Task SetLastUpdatedNow()
        {
            await StateManager.SetStateAsync(STATE_LAST_UPDATED_NAME, DateTimeOffset.UtcNow);
        }
    }
}
