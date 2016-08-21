using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Services.Communication.Client;
using SInnovations.ServiceFabric.GatewayService.Logging;

namespace SInnovations.ServiceFabric.GatewayService.Communication
{
    /// <summary>
    /// WebSockets communication client factory for StockService.
    /// </summary>
    public class WsCommunicationClientFactory : CommunicationClientFactoryBase<WsCommunicationClient>
    {
        private static readonly ILogger Logger = ApplicationLogging.CreateLogger<WsCommunicationClientFactory>();
        private static readonly TimeSpan MaxRetryBackoffIntervalOnNonTransientErrors = TimeSpan.FromSeconds(3);

        protected override bool ValidateClient(WsCommunicationClient client)
        {
            return client.ValidateClient();
        }

        protected override bool ValidateClient(string endpoint, WsCommunicationClient client)
        {
            return client.ValidateClient(endpoint);
        }

        protected override Task<WsCommunicationClient> CreateClientAsync(
            string endpoint,
            CancellationToken cancellationToken
            )
        {
            Logger.LogDebug("CreateClientAsync: {0}", endpoint);

             

            string endpointAddress = endpoint;
            if (!endpointAddress.EndsWith("/"))
            {
                endpointAddress = endpointAddress + "/";
            }

            // Create a communication client. This doesn't establish a session with the server.
            WsCommunicationClient client = new WsCommunicationClient(endpointAddress);
        //    await client.ConnectAsync(cancellationToken);

            return Task.FromResult( client );
        }

        protected override void AbortClient(WsCommunicationClient client)
        {
            // Http communication doesn't maintain a communication channel, so nothing to abort.
            Logger.LogDebug("AbortClient");
        }
    }
}
