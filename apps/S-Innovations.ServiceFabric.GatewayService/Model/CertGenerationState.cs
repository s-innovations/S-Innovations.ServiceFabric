using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using SInnovations.ServiceFabric.Gateway.Model;

namespace SInnovations.ServiceFabric.GatewayService.Model
{
    [DataContract]
    public class CertGenerationState
    {
        [DataMember]
        public bool Completed { get; set; }
        [DataMember]
        public string HostName { get; set; }
        [DataMember]
        public SslOptions SslOptions { get; set; }
    }
}
