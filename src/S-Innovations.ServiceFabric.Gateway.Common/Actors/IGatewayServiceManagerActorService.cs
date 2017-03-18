using Microsoft.ServiceFabric.Services.Remoting;
using SInnovations.ServiceFabric.Gateway.Common.Model;
using SInnovations.ServiceFabric.Gateway.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SInnovations.ServiceFabric.Gateway.Common.Actors
{
    public interface IGatewayServiceManagerActorService : IService
    {
        Task<IDictionary<long, DateTimeOffset>> GetLastUpdatedAsync(CancellationToken cancellationToken);
        Task<CertGenerationState> GetCertGenerationInfoAsync(string hostname, SslOptions options, CancellationToken cancellationToken);

        Task<List<GatewayServiceRegistrationData>> GetGatewayServicesAsync(CancellationToken cancellationToken);
        Task DeleteGatewayServiceAsync(string key, CancellationToken cancellationToken);
    }
}
