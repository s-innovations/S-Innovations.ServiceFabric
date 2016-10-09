using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;

namespace SInnovations.ServiceFabric.Gateway.Communication
{
   
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
            if (innerDispatcherProvider == null)
            {
                throw new ArgumentNullException(nameof(innerDispatcherProvider));
            }

            _innerDispatcherProvider = innerDispatcherProvider;
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
