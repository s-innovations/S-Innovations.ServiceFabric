using Microsoft.AspNetCore.Hosting;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System;
using System.Text;
using System.Linq;
using SInnovations.ServiceFabric.GatewayService.Services;
using Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Practices.Unity;
using SInnovations.ServiceFabric.Unity;

namespace SInnovations.ServiceFabric.GatewayService
{
    public class Program
    {
        // Entry point for the application.
        public static void Main(string[] args)
        {
            var log = new LoggerConfiguration()
                .MinimumLevel.Debug()
              //  .WriteTo.Trace()
                .CreateLogger();


            using (var container = new UnityContainer().AsFabricContainer())
            {
                var loggerfac = new LoggerFactory() as ILoggerFactory;
                loggerfac.AddSerilog();
                container.RegisterInstance(loggerfac);

                container.WithStatelessService<WebHostingService>("GatewayServiceType");

              
                Thread.Sleep(Timeout.Infinite);
            }
            
        }

       
    }
}
