using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace SInnovations.ServiceFabric.Gateway.Model
{

    [DataContract]
    public class SslOptions
    {
        [DataMember]
        public string SignerEmail { get; set; }
        [DataMember]
        public bool Enabled { get; set; }
    }
}
