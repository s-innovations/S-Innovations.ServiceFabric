﻿using System;
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
using Microsoft.WindowsAzure.Storage;
using SInnovations.ServiceFabric.Gateway.Actors;
using SInnovations.ServiceFabric.Gateway.Model;
using SInnovations.ServiceFabric.GatewayService.Configuration;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Services;
using SInnovations.ServiceFabric.Storage.Configuration;

namespace SInnovations.ServiceFabric.GatewayService.Services
{

    public static class NginxEx
    {
        public static IDictionary<string, List<GatewayServiceRegistrationData>> GroupByServerName(this List<GatewayServiceRegistrationData> proxies)
        {
            
            var servers = proxies.SelectMany(g =>
                        (g.ServerName ?? FabricRuntime.GetNodeContext().IPAddressOrFQDN)
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => new { sslKey=k+g.Ssl, key = k, value = g }))
                    .GroupBy(k => k.sslKey).ToDictionary(k => k.Key, k => new { hostname = k.First().key, locations = k.Select(v => v.value).ToList() });



            var singles = servers.Where(k => k.Value.locations.Count > 1)
                .ToDictionary(k => k.Value.hostname, v => v.Value.locations);

                        
            foreach (var combine in servers.Where(k => k.Value.locations.Count == 1).GroupBy(k => k.Value.locations.First()))
            {
                singles.Add(string.Join(" ", combine.Select(k => k.Value.hostname)), new List<GatewayServiceRegistrationData> { combine.Key });
            }   

            foreach(var test in singles.ToArray())
            {
                if (test.Value.Any(k => k.Ssl.Enabled) && test.Key.Contains(' '))
                {
                    foreach(var hostname in test.Key.Split(' '))
                    {
                        if (singles.ContainsKey(hostname))
                        {
                            singles[hostname].AddRange(test.Value);
                        }
                        else
                        {
                            singles.Add(hostname, test.Value);
                        }
                    }

                    singles.Remove(test.Key);
                }
            }
            

            return singles;
        }
    }
    /// <summary>
    /// A specialized stateless service for hosting ASP.NET Core web apps.
    /// </summary>
    public sealed class NginxGatewayService : KestrelHostingService<Startup>
    {
        


        private string nginxProcessName = "";
        
        private readonly StorageConfiguration Storage;
        private CloudStorageAccount storageAccount;

        public NginxGatewayService(StatelessServiceContext serviceContext, StorageConfiguration storage)
            : base(new KestrelHostingServiceOptions
            {
                ServiceEndpointName = "ServiceEndpoint1",
                GatewayOptions = new GatewayOptions
                {
                    Key = "NGINX-MANAGER",
                    ReverseProxyLocation = "/manage",
                    ServerName = "www.earthml.com local.earthml" 
                }

            }, serviceContext)
        {
            Storage = storage;
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
            var sslEndpoint = FabricRuntime.GetActivationContext().GetEndpoint("SslServiceEndpoint");
         
            var sb = new StringBuilder();

            sb.AppendLine("worker_processes  1;");
            sb.AppendLine("events {\n\tworker_connections  1024;\n}");
            sb.AppendLine("http {");

            File.WriteAllText("mime.types", WriteMimeTypes(sb, "mime.types").ToString());

            sb.AppendLine("\tkeepalive_timeout  65;");
            sb.AppendLine("\tgzip  on;");
            {
                var proxies = await actor.GetGatewayServicesAsync();



                foreach (var serverGroup in proxies.GroupByServerName())
                {
                    var serverName = serverGroup.Key;
                    var sslOn = serverName != "localhost" && serverGroup.Value.Any(k => k.Ssl.Enabled);
                    
                    if (sslOn)
                    {
                        sslOn = await actor.IsCertificateAvaibleAsync(serverName, serverGroup.Value.First().Ssl);
                    }
                   

                    sb.AppendLine("\tserver {");
                    {
                        sb.AppendLine($"\t\tlisten       {endpoint.Port};");
                        if (sslOn)
                        {
                            sb.AppendLine($"\t\tlisten       {sslEndpoint.Port} ssl;");
                        }

                        sb.AppendLine($"\t\tserver_name  {serverName};");
                        sb.AppendLine();

                        if (sslOn)
                        {

                            var certs = storageAccount.CreateCloudBlobClient().GetContainerReference("certs");

                            var certBlob = certs.GetBlockBlobReference($"{serverName}.crt");
                            var keyBlob = certs.GetBlockBlobReference($"{serverName}.key");


                            Directory.CreateDirectory(Path.Combine(Context.CodePackageActivationContext.WorkDirectory, "letsencrypt"));

                            await certBlob.DownloadToFileAsync($"{Context.CodePackageActivationContext.WorkDirectory}/letsencrypt/{serverName}.crt", FileMode.Create);
                            await keyBlob.DownloadToFileAsync($"{Context.CodePackageActivationContext.WorkDirectory}/letsencrypt/{serverName}.key", FileMode.Create);


                            sb.AppendLine($"\t\tssl_certificate {Context.CodePackageActivationContext.WorkDirectory}/letsencrypt/{serverName}.crt;");
                            sb.AppendLine($"\t\t ssl_certificate_key {Context.CodePackageActivationContext.WorkDirectory}/letsencrypt/{serverName}.key;");

                        }
                       

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

            storageAccount = await Storage.GetApplicationStorageAccountAsync();

            var gateway = ActorProxy.Create<IGatewayServiceManagerActor>(new ActorId(0));
            
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