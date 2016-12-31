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
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Runtime;
using SInnovations.ServiceFabric.Gateway.Actors;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Services;

namespace SInnovations.ServiceFabric.GatewayService.Services
{

    public static class NginxEx
    {
        public static IDictionary<string, List<GatewayEventData>> GroupByServerName(this List<GatewayEventData> proxies)
        {
            var servers = proxies.SelectMany(g =>
                        (g.ServerName ?? FabricRuntime.GetNodeContext().IPAddressOrFQDN)
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => new { key = k, value = g }))
                    .GroupBy(k => k.key).ToDictionary(k => k.Key, k => k.Select(v => v.value).ToList());

            var singles = servers.Where(k => k.Value.Count > 1).ToDictionary(k => k.Key, v => v.Value);
            foreach (var combine in servers.Where(k => k.Value.Count == 1).GroupBy(k => k.Value.First()))
            {
                singles.Add(string.Join(" ", combine.Select(k => k.Key)), new List<GatewayEventData> { combine.Key });
            }

            return singles;
        }
    }
    /// <summary>
    /// A specialized stateless service for hosting ASP.NET Core web apps.
    /// </summary>
    public sealed class NginxGatewayService : KestrelHostingService<Startup>, IGatewayNodeService
    {
        // private Dictionary<string, GatewayEventData> _proxies = new Dictionary<string, GatewayEventData>();

        //public async Task GameScoreUpdatedAsync(IGatewayServiceManagerActor actor, GatewayEventData data)
        //{
        //    try
        //    {


        //        await WriteConfigAsync(actor);
        //        //launchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\"");
        //        launchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\" -s reload");
        //        //    launchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\" -s quit");

        //    }
        //    catch (Exception ex)
        //    {

        //    }
        //}


        private string nginxProcessName = "";


        // private IWebHost _webHost;

        public NginxGatewayService(StatelessServiceContext serviceContext)
            : base(new KestrelHostingServiceOptions
            {
                ServiceEndpointName = "ServiceEndpoint1",
                ReverseProxyLocation = "/manage"
            }, serviceContext)
        {

        }

        #region StatelessService



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

        private async Task WriteConfigAsync(IGatewayServiceManagerActor actor)
        {
            var endpoint = FabricRuntime.GetActivationContext().GetEndpoint("ServiceEndpoint");
            string serverUrl = $"{endpoint.Protocol}://{FabricRuntime.GetNodeContext().IPAddressOrFQDN}:{endpoint.Port}";
            //  var endpoint1 = FabricRuntime.GetActivationContext().GetEndpoint("ServiceEndpoint1");
            // string serverUrl1 = $"{endpoint1.Protocol}://{FabricRuntime.GetNodeContext().IPAddressOrFQDN}:{endpoint1.Port}";

            var sb = new StringBuilder();

            sb.AppendLine("worker_processes  1;");
            sb.AppendLine("events {\n\tworker_connections  1024;\n}");
            sb.AppendLine("http {");

            File.WriteAllText("mime.types", WriteMimeTypes(sb, "mime.types").ToString());

            sb.AppendLine("\tkeepalive_timeout  65;");
            sb.AppendLine("\tgzip  on;");
            {
                var proxies = await actor.GetProxiesAsync();

               

                foreach (var serverGroup in proxies.GroupByServerName())
                {
                    var serverName = serverGroup.Key;

                    sb.AppendLine("\tserver {");
                    {
                        sb.AppendLine($"\t\tlisten       {endpoint.Port};");
                        sb.AppendLine($"\t\tserver_name  {serverName};");

                        foreach (var a in serverGroup.Value)
                        {
                            if (a.IPAddressOrFQDN == FabricRuntime.GetNodeContext().IPAddressOrFQDN)
                            {
                                WriteProxyPassLocation(2, a.ReverseProxyLocation, a.BackendPath, sb);
                            }
                        }
                    }
                    sb.AppendLine("\t}");
                }

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

        private static void WriteProxyPassLocation(int level, string location, string url, StringBuilder sb)
        {

            var tabs = string.Join("", Enumerable.Range(0, level + 1).Select(r => "\t"));
            sb.AppendLine($"{string.Join("", Enumerable.Range(0, level).Select(r => "\t"))}location {location} {{");
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
                if (!location.Trim().StartsWith("~"))
                    sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-PathBase   {location};");

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
            try
            {
                nginxProcessName = nginxProcess.ProcessName;
            }
            catch (Exception)
            {

            }
        }


        protected override Task OnCloseAsync(CancellationToken cancellationToken)
        {
            if (isNginxRunning())
                launchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\" -s quit");


            return base.OnCloseAsync(cancellationToken);
        }

        protected override Task OnOpenAsync(CancellationToken cancellationToken)
        {
            return base.OnOpenAsync(cancellationToken);
        }
        protected override void OnAbort()
        {
            if (isNginxRunning())
                launchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\" -s quit");



            base.OnAbort();
        }
        private DateTimeOffset lastWritten = DateTimeOffset.MinValue;
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {


            var gateway = ActorProxy.Create<IGatewayServiceManagerActor>(new ActorId(0));
            //await base.RunAsync(cancellationToken);


            //  await gateway.SubscribeAsync<IGatewayServiceMaanagerEvents>(this);

            //    await gateway.OnHostingNodeReadyAsync();


            await WriteConfigAsync(gateway);

            launchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\"");



            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    launchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\" -s quit");
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                if (!isNginxRunning())
                    launchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\"");

                var updated = await gateway.GetLastUpdatedAsync();
                if (!lastWritten.Equals(updated))
                {
                    lastWritten = updated;
                    await WriteConfigAsync(gateway);

                    launchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\" -s reload");
                }

            }




        }
        #endregion StatelessService


    }
}
