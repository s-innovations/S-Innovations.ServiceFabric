using Microsoft.AspNetCore.Hosting;

using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Communication
{
    public class CustomKestrelCommunicationListener : KestrelCommunicationListener
    {
        private readonly ServiceContext _serviceContext;
        public CustomKestrelCommunicationListener(ServiceContext serviceContext, string serviceEdpoint, Func<string, IWebHost> build) : base(serviceContext, serviceEdpoint, build)
        {
            _serviceContext = serviceContext;
        }

        public override async Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            var url = await base.OpenAsync(cancellationToken).ConfigureAwait(false);

            return url.Replace("[::]", _serviceContext.NodeContext.IPAddressOrFQDN);
        }
    }
}
