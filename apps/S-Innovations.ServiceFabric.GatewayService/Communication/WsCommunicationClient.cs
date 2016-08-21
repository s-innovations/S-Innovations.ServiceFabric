using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Services.Communication.Client;
using SInnovations.ServiceFabric.GatewayService.Logging;

namespace SInnovations.ServiceFabric.GatewayService.Communication
{
    /// <summary>
    /// Communication client that wraps the logic for talking to the StockService service.
    /// Created by communication client factory.
    /// </summary>
    public class WsCommunicationClient :  ICommunicationClient
    {
        private static readonly ILogger Logger = ApplicationLogging.CreateLogger<WsCommunicationClient>();
        public ClientWebSocket clientWebSocket = null;

        public WsCommunicationClient(string baseAddress)
        {
            Logger.LogDebug("ctor: {0}", baseAddress);

            this.clientWebSocket = new ClientWebSocket();

            this.BaseAddress = baseAddress;
        }

        /// <summary>
        /// Base address of the client
        /// </summary>
        public string BaseAddress { get; }

        /// <summary>
        /// The resolved service partition which contains the resolved service endpoints.
        /// </summary>
        public ResolvedServicePartition ResolvedServicePartition { get; set; }

        public string ListenerName { get; set; }

        public ResolvedServiceEndpoint Endpoint { get; set; }

        /// <summary>
        /// 
        /// </summary>
        internal bool ValidateClient()
        {
            if (this.clientWebSocket == null)
            {
                return false;
            }

            if (this.clientWebSocket.State != WebSocketState.Open && this.clientWebSocket.State != WebSocketState.Connecting)
            {
                this.clientWebSocket.Dispose();
                this.clientWebSocket = null;
                return false;
            }

            return true;
        }

        internal bool ValidateClient(string endpoint)
        {
            if (this.BaseAddress == endpoint)
            {
                return true;
            }

            this.clientWebSocket.Dispose();
            this.clientWebSocket = null;
            return false;
        }

        internal async Task ConnectAsync(HttpContext context, CancellationToken cancellationToken)
        {
            var uri = new UriBuilder(BaseAddress);

            var hadDefaultPort = uri.Uri.IsDefaultPort;
            uri.Scheme = "ws";
            uri.Port = hadDefaultPort ? -1 : uri.Port;
            uri.Path = context.Request.Path;

            if (context.Request.QueryString.HasValue)
                uri.Query = context.Request.QueryString.Value.Substring(1);

            if (context.Request.Cookies.Any())
            {
                this.clientWebSocket.Options.Cookies = new System.Net.CookieContainer();
            }

            foreach (var cookie in context.Request.Cookies)
                this.clientWebSocket.Options.Cookies.Add( new System.Net.Cookie(cookie.Key, cookie.Value,"/",context.Request.Host.Host));

           
         
            await this.clientWebSocket.ConnectAsync(uri.Uri, cancellationToken);
        }
    }
}
