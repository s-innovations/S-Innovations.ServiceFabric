using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using SInnovations.ServiceFabric.Gateway.Model;

namespace SInnovations.ServiceFabric.Gateway.Actors
{
   
  

    

    public interface IGatewayServiceManagerActor : IActor
    {
        /// <summary>
        /// Register a backend service to be configured to receive requests from the proxy.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        Task RegisterGatewayServiceAsync(GatewayServiceRegistrationData data);  
        
        /// <summary>
        /// Get all registered Proxies
        /// </summary>
        /// <returns></returns>
                      
        Task<List<GatewayServiceRegistrationData>> GetGatewayServicesAsync();
        /// <summary>
        /// Get the last time an update was made that should cause configuration files to be rewritten
        /// </summary>
        /// <returns></returns>
     //   Task<DateTimeOffset> GetLastUpdatedAsync();

        /// <summary>
        /// Check if a certificate is ready for the given hostname, if not the certificate will be requested for later checks.
        /// </summary>
        /// <param name="hostname"></param>
        /// <param name="options"></param>
        /// <returns></returns>
       // Task<bool> IsCertificateAvaibleAsync(string hostname, SslOptions options);
        Task RequestCertificateAsync(string hostname, SslOptions options);

        Task SetupStorageServiceAsync(int instanceCount);
    }
}
