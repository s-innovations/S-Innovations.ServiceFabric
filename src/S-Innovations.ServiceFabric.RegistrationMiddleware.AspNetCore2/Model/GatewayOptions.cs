using SInnovations.ServiceFabric.Gateway.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Model
{
    public class GatewayOptions
    {
        public string Key { get; set; }
        public string ReverseProxyLocation { get; set; }
        public string ServerName { get; set; }
        public SslOptions Ssl { get; set; } = new SslOptions();
    }
}
