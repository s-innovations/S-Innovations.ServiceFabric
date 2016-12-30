using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace SInnovations.ServiceFabric.Gateway.Actors
{
    public interface IGatewayNodeService
    {

    }


    public class GatewayEventData
    {
        public string ForwardPath { get; set; }
        public string BackendPath { get; set; }
    }

    public interface IGatewayServiceMaanagerEvents : IActorEvents
    {
        void GameScoreUpdated(IGatewayServiceManagerActor actor , GatewayEventData data);
    }

    public interface IGatewayServiceManagerActor : IActor, IActorEventPublisher<IGatewayServiceMaanagerEvents>
    {
        Task OnHostOpenAsync(GatewayEventData data);
        Task OnHostingNodeReadyAsync();
    }
}
