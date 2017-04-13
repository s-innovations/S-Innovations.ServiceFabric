using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace SInnovations.ServiceFabric.Gateway.Communication
{

    public class HttpCommunicationServicePartitionClient : ServicePartitionClient<HttpCommunicationClient>
    {
        private readonly FabricClient fabricClient;
        private readonly Uri application;
        public HttpCommunicationServicePartitionClient(
            FabricClient fabricClient, Uri application, Uri serviceUri, ServicePartitionKey partitionKey = null, TargetReplicaSelector targetReplicaSelector = TargetReplicaSelector.Default, string listenerName = null, OperationRetrySettings retrySettings = null)
            : base(new HttpCommunicationClientFactory(new ServicePartitionResolver(() => fabricClient)), serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings)
        {

            this.fabricClient = fabricClient;
            this.application = application;

        }

        public string BearerToken { get; set; }


        public Task<HttpResponseMessage> GetAsync(string pathAndQuery)
        {
            return InvokeWithRetryAsync(async (client) =>
            {
                if (!string.IsNullOrEmpty(BearerToken))
                {
                    client.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", BearerToken);
                }

                var services = await fabricClient.QueryManager.GetServiceListAsync(application, ServiceUri).ConfigureAwait(false);
                var service = services.FirstOrDefault();
                var key = $"{ServiceUri.AbsoluteUri.Substring("fabric:/".Length)}/{service.ServiceManifestVersion}";

                client.DefaultRequestHeaders.Add("X-ServiceFabric-Key", key);


                HttpResponseMessage response = await client.GetAsync(new Uri(client.BaseAddress, pathAndQuery));
                return response;
            });
        }



    }

    public class HttpCommunicationClientFactory : CommunicationClientFactoryBase<HttpCommunicationClient>
    {
        private readonly Func<HttpCommunicationClient> _innerDispatcherProvider;

        public HttpCommunicationClientFactory(IServicePartitionResolver servicePartitionResolver = null, IEnumerable<IExceptionHandler> exceptionHandlers = null, string traceId = null)
            : this(() => new HttpCommunicationClient(), servicePartitionResolver, exceptionHandlers, traceId)
        {          
        }

        public HttpCommunicationClientFactory(Func<HttpCommunicationClient> innerDispatcherProvider, IServicePartitionResolver servicePartitionResolver = null, IEnumerable<IExceptionHandler> exceptionHandlers = null, string traceId = null)
            : base(servicePartitionResolver, exceptionHandlers, traceId)
        {
            _innerDispatcherProvider = innerDispatcherProvider ?? throw new ArgumentNullException(nameof(innerDispatcherProvider));
        }

        protected override void AbortClient(HttpCommunicationClient dispatcher)
        {
            if (dispatcher != null)
            {
                dispatcher.Dispose();
            }
        }

        protected override Task<HttpCommunicationClient> CreateClientAsync(string endpoint, CancellationToken cancellationToken)
        {
            var dispatcher = _innerDispatcherProvider.Invoke();
            dispatcher.BaseAddress = new Uri(endpoint, UriKind.Absolute);

            return Task.FromResult(dispatcher);
        }

        protected override bool ValidateClient(HttpCommunicationClient dispatcher)
        {
            return dispatcher != null && dispatcher.BaseAddress != null;
        }

        protected override bool ValidateClient(string endpoint, HttpCommunicationClient dispatcher)
        {
            return dispatcher != null && dispatcher.BaseAddress == new Uri(endpoint, UriKind.Absolute);
        }
    }
}
