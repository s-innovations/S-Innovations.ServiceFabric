using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Client;

namespace SInnovations.ServiceFabric.Gateway.Communication
{

    public class HttpCommunicationClient : HttpClient, ICommunicationClient
    {
        public HttpCommunicationClient()
            : base(new HttpClientHandler() { AllowAutoRedirect = false, UseCookies = false })
        {
        }

        public HttpCommunicationClient(HttpMessageHandler handler)
            : base(handler)
        {
        }

        public HttpCommunicationClient(HttpMessageHandler handler, bool disposeHandler)
            : base(handler, disposeHandler)
        {
        }

        #region ICommunicationClient

        string ICommunicationClient.ListenerName { get; set; }

        ResolvedServiceEndpoint ICommunicationClient.Endpoint { get; set; }

        ResolvedServicePartition ICommunicationClient.ResolvedServicePartition { get; set; }

        #endregion ICommunicationClient
    }
}
