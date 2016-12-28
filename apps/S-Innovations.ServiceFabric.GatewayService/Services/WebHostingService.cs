using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace SInnovations.ServiceFabric.GatewayService.Services
{
    /// <summary>
    /// A specialized stateless service for hosting ASP.NET Core web apps.
    /// </summary>
    public sealed class WebHostingService : StatelessService, ICommunicationListener
    {




        private string nginxProcessName = "";

        private IWebHost _webHost;

        public WebHostingService(StatelessServiceContext serviceContext)
            : base(serviceContext)
        {

        }

        #region StatelessService

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new[] { new ServiceInstanceListener(_ => this) };
        }

        private bool isNginxRunning()
        {
            if (!string.IsNullOrEmpty(nginxProcessName))
            {
                var processes = Process.GetProcessesByName(nginxProcessName);
                return processes.Length != 0;
            }
            else
                return false;
        }

        private void WriteConfig()
        {
            var endpoint = FabricRuntime.GetActivationContext().GetEndpoint("ServiceEndpoint");
            string serverUrl = $"{endpoint.Protocol}://{FabricRuntime.GetNodeContext().IPAddressOrFQDN}:{endpoint.Port}";
            var endpoint1 = FabricRuntime.GetActivationContext().GetEndpoint("ServiceEndpoint1");
            string serverUrl1 = $"{endpoint1.Protocol}://{FabricRuntime.GetNodeContext().IPAddressOrFQDN}:{endpoint1.Port}";

            var sb = new StringBuilder();

            sb.AppendLine("worker_processes  1;");
            sb.AppendLine("events {\n\tworker_connections  1024;\n}");
            sb.AppendLine("http {");

            File.WriteAllText("mime.types", WriteMimeTypes(sb, "mime.types").ToString());

            sb.AppendLine("\tkeepalive_timeout  65;");
            sb.AppendLine("\tgzip  on;");
            {
                sb.AppendLine("\tserver {");
                {
                    sb.AppendLine($"\t\tlisten       {endpoint.Port};");
                    sb.AppendLine($"\t\tserver_name  {FabricRuntime.GetNodeContext().IPAddressOrFQDN};");
                    WriteProxyPassLocation(2, "/manage", serverUrl1, sb);

                }
                sb.AppendLine("\t}");
            }
            sb.AppendLine("}");

            File.WriteAllText("nginx.conf", sb.ToString());
        }

        private static StringBuilder WriteMimeTypes(StringBuilder sb, string name)
        {
            var mime = new StringBuilder();
            sb.AppendLine($"\tinclude {name};");
            sb.AppendLine("\tdefault_type application/octet-stream;");
            mime.AppendLine("types{");
            foreach (var type in Constants.ExtensionMapping.GroupBy(kv => kv.Value, kv => kv.Key))
            {
                mime.AppendLine($"\t{type.Key} {string.Join(" ", type.Select(t => t.Trim('.')))};");
            }
            mime.AppendLine("}");

            return mime;

        }

        private static void WriteProxyPassLocation(int level, string pathPrefix, string url, StringBuilder sb)
        {

            var tabs = string.Join("", Enumerable.Range(0, level + 1).Select(r => "\t"));
            sb.AppendLine($"{string.Join("", Enumerable.Range(0, level).Select(r => "\t"))}location {pathPrefix} {{");
            {
                sb.AppendLine($"{tabs}proxy_pass {url};");
                sb.AppendLine($"{tabs}proxy_set_header Upgrade $http_upgrade;");
                sb.AppendLine($"{tabs}proxy_set_header Connection keep-alive;");

                sb.AppendLine($"{tabs}proxy_set_header Host					  $host;");
                sb.AppendLine($"{tabs}proxy_set_header X-Real-IP              $remote_addr;");
                sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-For        $proxy_add_x_forwarded_for;");
                sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-Host       $host;");
                sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-Server     $host;");
                sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-Proto      $scheme;");
                sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-Path       $request_uri;");
                sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-PathBase   {pathPrefix};");

                sb.AppendLine($"{tabs}proxy_cache_bypass $http_upgrade;");
            }
            sb.AppendLine($"{string.Join("", Enumerable.Range(0, level).Select(r => "\t"))}}}");



        }

        private void launchNginxProcess(string arguments)
        {
            var codePackage = this.Context.CodePackageActivationContext.CodePackageName;
            var configPackage = this.Context.CodePackageActivationContext.GetConfigurationPackageObject("Config");
            var codePath = this.Context.CodePackageActivationContext.GetCodePackageObject(codePackage).Path;
            var res = File.Exists(Path.Combine(codePath, "nginx-1.11.3.exe"));
            var nginxStartInfo = new ProcessStartInfo(Path.Combine(codePath, "nginx-1.11.3.exe"));
            nginxStartInfo.WorkingDirectory = codePath;
            nginxStartInfo.UseShellExecute = false;
            nginxStartInfo.Arguments = arguments;
            var nginxProcess = new Process();
            nginxProcess.StartInfo = nginxStartInfo;
            nginxProcess.Start();
            nginxProcessName = nginxProcess.ProcessName;
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            WriteConfig();
            launchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\"");


            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    launchNginxProcess("-s quit");
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                if (!isNginxRunning())
                    launchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\"");
            }


        }
        #endregion StatelessService

        #region ICommunicationListener

        void ICommunicationListener.Abort()
        {
            _webHost?.Dispose();
        }

        Task ICommunicationListener.CloseAsync(CancellationToken cancellationToken)
        {
            _webHost?.Dispose();

            return Task.FromResult(true);
        }
        public void ConfigureServices(IServiceCollection services)
        {
           

        }
            Task<string> ICommunicationListener.OpenAsync(CancellationToken cancellationToken)
        {

            var endpoint = FabricRuntime.GetActivationContext().GetEndpoint("ServiceEndpoint1");

            string serverUrl = $"{endpoint.Protocol}://{FabricRuntime.GetNodeContext().IPAddressOrFQDN}:{endpoint.Port}";

            _webHost = new WebHostBuilder().UseKestrel()
                                           .ConfigureServices(ConfigureServices)
                                           .UseContentRoot(Directory.GetCurrentDirectory())
                                           .UseStartup<Startup>()
                                           .UseUrls(serverUrl)
                                           .Build();

            _webHost.Start();

            return Task.FromResult(serverUrl);
        }

        #endregion ICommunicationListener
    }
}
