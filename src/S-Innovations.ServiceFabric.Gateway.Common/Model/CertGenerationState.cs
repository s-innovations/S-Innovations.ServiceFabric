using SInnovations.ServiceFabric.Gateway.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.ServiceFabric.Gateway.Common.Model
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
        [DataMember]
        public DateTimeOffset? RunAt { get; set; }
    }
}
