using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SInnovations.ServiceFabric.Gateway.Model
{
    public class GatewayServiceInformation
    {
        public string PathPrefix { get; set; }
        public bool StickySession { get; set; }

        public Uri GatewaySericeName { get; set; }
    }
}
