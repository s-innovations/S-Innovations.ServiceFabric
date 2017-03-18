using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Query;
using Microsoft.ServiceFabric.Actors.Runtime;
using SInnovations.ServiceFabric.Gateway.Common.Actors;
using SInnovations.ServiceFabric.Gateway.Common.Model;
using SInnovations.ServiceFabric.Gateway.Model;
using SInnovations.ServiceFabric.GatewayService.Actors;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;

namespace SInnovations.ServiceFabric.GatewayService.Services
{
    public class GatewayServiceManagerActorService : ActorService, IGatewayServiceManagerActorService
    {
        public GatewayServiceManagerActorService(
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
}
