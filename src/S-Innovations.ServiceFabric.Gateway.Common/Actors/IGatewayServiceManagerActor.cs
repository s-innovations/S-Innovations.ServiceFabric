using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace SInnovations.ServiceFabric.Gateway.Actors
{
    public interface IGatewayNodeService
    {

    }


    [DataContract]
    public class GatewayEventData
    {
        [DataMember]
        public string ReverseProxyLocation { get; set; }
        [DataMember]
        public string BackendPath { get; set; }
        [DataMember]
        public string IPAddressOrFQDN { get; set; }

        [DataMember]
        public string ServerName { get; set; }
    }

    public interface IGatewayServiceMaanagerEvents : IActorEvents
    {
        Task GameScoreUpdatedAsync(IGatewayServiceManagerActor actor , GatewayEventData data);
    }

    public interface IGatewayServiceManagerActor : IActor
    {
        Task OnHostOpenAsync(GatewayEventData data);
        Task OnHostingNodeReadyAsync();

        Task<List<GatewayEventData>> GetProxiesAsync();
        Task<DateTimeOffset> GetLastUpdatedAsync();
    }
}
