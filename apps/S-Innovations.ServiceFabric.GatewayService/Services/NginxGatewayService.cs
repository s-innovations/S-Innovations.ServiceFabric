using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Fabric.Description;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Practices.Unity;
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
using SInnovations.ServiceFabric.GatewayService.Actors;
using Microsoft.ServiceFabric.Services.Client;
using SInnovations.ServiceFabric.GatewayService.Model;
using SInnovations.ServiceFabric.Gateway.Common.Model;
using SInnovations.ServiceFabric.Gateway.Common.Actors;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Model;

namespace SInnovations.ServiceFabric.GatewayService.Services
{

    public static class NginxEx
    {
        public static IDictionary<string, List<GatewayServiceRegistrationData>> GroupByServerName(this List<GatewayServiceRegistrationData> proxies)
        {

            var servers = proxies.SelectMany(g =>
                        (g.ServerName ?? FabricRuntime.GetNodeContext().IPAddressOrFQDN)
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => new { sslKey = k + g.Ssl, key = k, value = g }))
                    .GroupBy(k => k.sslKey).ToDictionary(k => k.Key, k => new { hostname = k.First().key, locations = k.Select(v => v.value).ToList() });



            var singles = servers.Where(k => k.Value.locations.Count > 1)
                .ToDictionary(k => k.Value.hostname, v => v.Value.locations);


            foreach (var combine in servers.Where(k => k.Value.locations.Count == 1).GroupBy(k => k.Value.locations.First()))
            {
                singles.Add(string.Join(" ", combine.Select(k => k.Value.hostname)), new List<GatewayServiceRegistrationData> { combine.Key });
            }

            foreach (var test in singles.ToArray())
            {
                if (test.Value.Any(k => k.Ssl.Enabled) && test.Key.Contains(' '))
                {
                    foreach (var hostname in test.Key.Split(' '))
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
        private const string nginxVersion = "nginx-1.11.13.exe";
        private string nginxProcessName = "";

        private readonly StorageConfiguration Storage;
        private CloudStorageAccount storageAccount;
        private readonly ILogger _logger;

        private readonly FabricClient _fabricClient = new FabricClient();

        public NginxGatewayService(StatelessServiceContext serviceContext, IUnityContainer container, ILoggerFactory factory, StorageConfiguration storage)
            : base(new KestrelHostingServiceOptions
            {
                // ServiceEndpointName = "PrivateManageServiceEndpoint",
                GatewayOptions = new GatewayOptions
                {
                    Key = "NGINX-MANAGER",
                    ReverseProxyLocation = "/manage/",
                    ServerName = "www.earthml.com local.earthml.com",
                    Ssl = new SslOptions
                    {
                        Enabled = true,
                        SignerEmail = "info@earthml.com"
                    }
                }

            }, serviceContext, factory, container)
        {
            Storage = storage;
            _logger = factory.CreateLogger<NginxGatewayService>();
        }

        #region StatelessService


        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(this);
            base.ConfigureServices(services);
        }

        private bool IsNginxRunning()
        {
            if (!string.IsNullOrEmpty(nginxProcessName))
            {
                var processes = Process.GetProcessesByName(nginxProcessName);
                return processes.Length != 0;
            }
            else
                return false;
        }

        private async Task WriteConfigAsync(CancellationToken token)
        {
            var endpoint = FabricRuntime.GetActivationContext().GetEndpoint("NginxServiceEndpoint");
            var sslEndpoint = FabricRuntime.GetActivationContext().GetEndpoint("NginxSslServiceEndpoint");

            var sb = new StringBuilder();

            sb.AppendLine("worker_processes  1;");
            sb.AppendLine("events {\n\tworker_connections  1024;\n}");
            sb.AppendLine("http {");



            File.WriteAllText("mime.types", WriteMimeTypes(sb, "mime.types").ToString());

            sb.AppendLine("\tkeepalive_timeout          65;");
            sb.AppendLine("\tgzip                       on;");
            sb.AppendLine("\tproxy_buffer_size          128k;");
            sb.AppendLine("\tproxy_buffers              4 256k;");
            sb.AppendLine("\tproxy_busy_buffers_size    256k;");
          


            {
                var proxies = await GetGatewayServicesAsync(token);


                foreach (var upstreams in proxies.GroupBy(k => k.ServiceName))
                {
                    var hashset = new HashSet<string>();

                    var uniques = upstreams.Where(upstream => {
                        var added = hashset.Contains(new Uri(upstream.BackendPath).Authority);
                        if (!added) { hashset.Add(new Uri(upstream.BackendPath).Authority); }
                        return !added;
                        }).ToArray();

                    var upstreamName = upstreams.Key.AbsoluteUri.Split('/').Last().Replace('.', '_');
                    //$upstream_addr
                    sb.AppendLine($"\tupstream {upstreamName} {{");
                    foreach (var upstream in uniques)
                    {

                        sb.AppendLine($"\t\tserver {new Uri(upstream.BackendPath).Authority.Replace("localhost", "127.0.0.1")} {(upstream.IPAddressOrFQDN != Context.NodeContext.IPAddressOrFQDN ? "backup" : "")};");
                    }
                    sb.AppendLine("\t}");

                    //sb.AppendLine($"\tmap $upstream_addr ${upstreamName}uniquepath {{");
                    ////sb.AppendLine("\t\tdefault          $upstream_addr;");
                    //foreach (var upstream in uniques)
                    //{
                    //    sb.AppendLine($"\t\t{new Uri(upstream.BackendPath).Authority.Replace("localhost","127.0.0.1")} \"{new Uri(upstream.BackendPath).AbsolutePath.Trim('/')}\";");
                    //}
                    //sb.AppendLine("\t}");
                }

                foreach (var serverGroup in proxies.GroupByServerName())
                {
                    var serverName = serverGroup.Key;
                    var sslOn = serverName != "localhost" && serverGroup.Value.Any(k => k.Ssl.Enabled);

                    if (sslOn)
                    {
                        var state = await GetCertGenerationStateAsync(serverName, serverGroup.Value.First().Ssl, false, token);
                        sslOn = state != null && state.Completed;
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
                            var chainBlob = certs.GetBlockBlobReference($"{serverName}.fullchain.pem");

                            Directory.CreateDirectory(Path.Combine(Context.CodePackageActivationContext.WorkDirectory, "letsencrypt"));

                            await keyBlob.DownloadToFileAsync($"{Context.CodePackageActivationContext.WorkDirectory}/letsencrypt/{serverName}.key", FileMode.Create);

                            if (await chainBlob.ExistsAsync())
                            {
                                await chainBlob.DownloadToFileAsync($"{Context.CodePackageActivationContext.WorkDirectory}/letsencrypt/{serverName}.fullchain.pem", FileMode.Create);
                                sb.AppendLine($"\t\tssl_certificate {Context.CodePackageActivationContext.WorkDirectory}/letsencrypt/{serverName}.fullchain.pem;");

                            }
                            else
                            {
                                await GetCertGenerationStateAsync(serverName, serverGroup.Value.First().Ssl, true, token);
                                await certBlob.DownloadToFileAsync($"{Context.CodePackageActivationContext.WorkDirectory}/letsencrypt/{serverName}.crt", FileMode.Create);
                                sb.AppendLine($"\t\tssl_certificate {Context.CodePackageActivationContext.WorkDirectory}/letsencrypt/{serverName}.crt;");
                            }

                            sb.AppendLine($"\t\tssl_certificate_key {Context.CodePackageActivationContext.WorkDirectory}/letsencrypt/{serverName}.key;");

                            sb.AppendLine($"\t\tssl_session_timeout  5m;");

                            sb.AppendLine($"\t\tssl_prefer_server_ciphers on;");
                            sb.AppendLine($"\t\tssl_protocols TLSv1 TLSv1.1 TLSv1.2;");
                            sb.AppendLine($"\t\tssl_ciphers 'ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-AES256-GCM-SHA384:DHE-RSA-AES128-GCM-SHA256:DHE-DSS-AES128-GCM-SHA256:kEDH+AESGCM:ECDHE-RSA-AES128-SHA256:ECDHE-ECDSA-AES128-SHA256:ECDHE-RSA-AES128-SHA:ECDHE-ECDSA-AES128-SHA:ECDHE-RSA-AES256-SHA384:ECDHE-ECDSA-AES256-SHA384:ECDHE-RSA-AES256-SHA:ECDHE-ECDSA-AES256-SHA:DHE-RSA-AES128-SHA256:DHE-RSA-AES128-SHA:DHE-DSS-AES128-SHA256:DHE-RSA-AES256-SHA256:DHE-DSS-AES256-SHA:DHE-RSA-AES256-SHA:AES128-GCM-SHA256:AES256-GCM-SHA384:AES128-SHA256:AES256-SHA256:AES128-SHA:AES256-SHA:AES:CAMELLIA:DES-CBC3-SHA:!aNULL:!eNULL:!EXPORT:!DES:!RC4:!MD5:!PSK:!aECDH:!EDH-DSS-DES-CBC3-SHA:!EDH-RSA-DES-CBC3-SHA:!KRB5-DES-CBC3-SHA';");
                            sb.AppendLine($"\t\tadd_header Strict-Transport-Security max-age=15768000;");

                        }


                        foreach (var a in serverGroup.Value)
                        {
                            if (a.IPAddressOrFQDN == Context.NodeContext.IPAddressOrFQDN)
                            {
                                var upstreamName = a.ServiceName.AbsoluteUri.Split('/').Last().Replace('.','_');

                                var url = a.BackendPath;
                                url = "http://" + upstreamName;

                                WriteProxyPassLocation(2, a.ReverseProxyLocation, url, sb, $"\"{a.ServiceName.AbsoluteUri.Substring("fabric:/".Length)}/{a.ServiceVersion}\"");
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

        private static void WriteProxyPassLocation(int level, string location, string url, StringBuilder sb,string uniquekey)
        {

            var tabs = string.Join("", Enumerable.Range(0, level + 1).Select(r => "\t"));
            sb.AppendLine($"{string.Join("", Enumerable.Range(0, level).Select(r => "\t"))}location {location} {{");
            {
                // rewrite ^ /268be5f6-90b1-4aa1-9eac-2225d8f7ab29/131356467681395031/$1 break;
                if (location.StartsWith("~"))
                {
                    var uri = new Uri(url);

                    if (!string.IsNullOrEmpty(uri.AbsolutePath?.TrimEnd('/')))
                    {
                        sb.AppendLine($"{tabs}rewrite ^ {uri.AbsolutePath}$uri break;");
                    }

                    sb.AppendLine($"{tabs}proxy_pass {uri.GetLeftPart(UriPartial.Authority)};");
                }
                else
                {
                    sb.AppendLine($"{tabs}proxy_pass {url.TrimEnd('/')}/;");
                }

               
                //  sb.AppendLine($"{tabs}proxy_redirect off;");
                sb.AppendLine($"{tabs}server_name_in_redirect on;");
                sb.AppendLine($"{tabs}port_in_redirect off;");


                sb.AppendLine($"{tabs}proxy_set_header Upgrade $http_upgrade;");
                sb.AppendLine($"{tabs}proxy_set_header Connection keep-alive;");
                //
                sb.AppendLine($"{tabs}proxy_set_header Host					  $host;");
                sb.AppendLine($"{tabs}proxy_set_header X-Real-IP              $remote_addr;");
                sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-For        $proxy_add_x_forwarded_for;");
                sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-Host       $host;");
                sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-Server     $host;");
                sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-Proto      $scheme;");
                sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-Path       $request_uri;");
                sb.AppendLine($"{tabs}proxy_set_header X-ServiceFabric-Key    {uniquekey};");

                sb.AppendLine($"{tabs}proxy_connect_timeout                   3s;");


                if (location.Trim().StartsWith("~"))
                    sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-PathBase   /;");
                else
                {
                    sb.AppendLine($"{tabs}proxy_set_header X-Forwarded-PathBase   {location};");
           
                }

                sb.AppendLine($"{tabs}proxy_cache_bypass $http_upgrade;");
            }
            sb.AppendLine($"{string.Join("", Enumerable.Range(0, level).Select(r => "\t"))}}}");



        }



        private void LaunchNginxProcess(string arguments)
        {
            var codePackage = this.Context.CodePackageActivationContext.CodePackageName;
            var configPackage = this.Context.CodePackageActivationContext.GetConfigurationPackageObject("Config");
            var codePath = this.Context.CodePackageActivationContext.GetCodePackageObject(codePackage).Path;
            var res = File.Exists(Path.Combine(codePath, nginxVersion));
            var nginxStartInfo = new ProcessStartInfo(Path.Combine(codePath, nginxVersion))
            {
                WorkingDirectory = codePath,
                UseShellExecute = false,
                Arguments = arguments
            };
            var nginxProcess = new Process()
            {
                StartInfo = nginxStartInfo
            };
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
            if (IsNginxRunning())
                LaunchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\" -s quit");


            return base.OnCloseAsync(cancellationToken);
        }

        protected override async Task OnOpenAsync(CancellationToken cancellationToken)
        {
            await base.OnOpenAsync(cancellationToken);
        }
        protected override void OnAbort()
        {
            if (IsNginxRunning())
                LaunchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\" -s quit");



            base.OnAbort();
        }
        private DateTimeOffset lastWritten = DateTimeOffset.MinValue;


        public async Task DeleteGatewayServiceAsync(string v, CancellationToken cancellationToken)
        {
            var applicationName = this.Context.CodePackageActivationContext.ApplicationName;
            var actorServiceUri = new Uri($"{applicationName}/GatewayServiceManagerActorService");
            List<long> partitions = await GetPartitionsAsync(actorServiceUri);
            var serviceProxyFactory = new ServiceProxyFactory();

          
            foreach (var partition in partitions)
            {
                var actorService = serviceProxyFactory.CreateServiceProxy<IGatewayServiceManagerActorService>(actorServiceUri, new ServicePartitionKey(partition));
                await actorService.DeleteGatewayServiceAsync(v,cancellationToken);
            }

        }
        public async Task<List<GatewayServiceRegistrationData>> GetGatewayServicesAsync(CancellationToken cancellationToken)
        {
            var applicationName = this.Context.CodePackageActivationContext.ApplicationName;
            var actorServiceUri = new Uri($"{applicationName}/GatewayServiceManagerActorService");
            List<long> partitions = await GetPartitionsAsync(actorServiceUri);

            var serviceProxyFactory = new ServiceProxyFactory();

            var all = new List<GatewayServiceRegistrationData>();
            foreach (var partition in partitions)
            {
                var actorService = serviceProxyFactory.CreateServiceProxy<IGatewayServiceManagerActorService>(actorServiceUri, new ServicePartitionKey(partition));

                var state = await actorService.GetGatewayServicesAsync(cancellationToken);
                all.AddRange(state);

            }
            return all;
        }
         
        private async Task<List<long>> GetPartitionsAsync(Uri actorServiceUri)
        {
            var partitions = new List<long>();
            var servicePartitionList = await _fabricClient.QueryManager.GetPartitionListAsync(actorServiceUri);
            foreach (var servicePartition in servicePartitionList)
            {
                var partitionInformation = servicePartition.PartitionInformation as Int64RangePartitionInformation;
                partitions.Add(partitionInformation.LowKey);
            }

            return partitions;
        }

        public async Task<CertGenerationState> GetCertGenerationStateAsync(string hostname, SslOptions options, bool force, CancellationToken token)
        {
            if (!force)
            {
                var applicationName = this.Context.CodePackageActivationContext.ApplicationName;
                var actorServiceUri = new Uri($"{applicationName}/GatewayServiceManagerActorService");
                List<long> partitions = await GetPartitionsAsync(actorServiceUri);

                var serviceProxyFactory = new ServiceProxyFactory();

                var actors = new Dictionary<long, DateTimeOffset>();
                foreach (var partition in partitions)
                {
                    var actorService = serviceProxyFactory.CreateServiceProxy<IGatewayServiceManagerActorService>(actorServiceUri, new ServicePartitionKey(partition));

                    var state = await actorService.GetCertGenerationInfoAsync(hostname, options, token);
                    if (state != null && state.RunAt.HasValue && state.RunAt.Value > DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(14)))
                    {
                        return state;
                    }

                }
            }

            var gateway = ActorProxy.Create<IGatewayServiceManagerActor>(new ActorId(0));
            await gateway.RequestCertificateAsync(hostname, options);

            return null;
        }
        public async Task SetLastUpdatedAsync(DateTimeOffset time, CancellationToken token)
        {
      
            var gateway = ActorProxy.Create<IGatewayServiceManagerActor>(new ActorId(0));
            await gateway.SetLastUpdatedNow();
           
        }
        public async Task<IDictionary<long, DateTimeOffset>> GetLastUpdatedAsync(CancellationToken token)
        {

            var applicationName = this.Context.CodePackageActivationContext.ApplicationName;
            var actorServiceUri = new Uri($"{applicationName}/GatewayServiceManagerActorService");
            List<long> partitions = await GetPartitionsAsync(actorServiceUri);

            var serviceProxyFactory = new ServiceProxyFactory();

            var actors = new Dictionary<long, DateTimeOffset>();
            foreach (var partition in partitions)
            {
                var actorService = serviceProxyFactory.CreateServiceProxy<IGatewayServiceManagerActorService>(actorServiceUri, new ServicePartitionKey(partition));

                var counts = await actorService.GetLastUpdatedAsync(token);
                foreach (var count in counts)
                {
                    actors.Add(count.Key, count.Value);
                }
            }
            return actors;
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {


            try
            {

                storageAccount = await Storage.GetApplicationStorageAccountAsync();

                var gateway = ActorProxy.Create<IGatewayServiceManagerActor>(new ActorId(0));
                var a = await _fabricClient.ServiceManager.GetServiceDescriptionAsync(this.Context.ServiceName) as StatelessServiceDescription;

                await gateway.SetupStorageServiceAsync(a.InstanceCount);
                await WriteConfigAsync(cancellationToken);

                LaunchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\"");



                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                        LaunchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\" -s quit");
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                    if (!IsNginxRunning())
                        LaunchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\"");

                    var allActorsUpdated = await GetLastUpdatedAsync(cancellationToken);
                    if (allActorsUpdated.ContainsKey(gateway.GetActorId().GetLongId()))
                    {
                        var updated = allActorsUpdated[gateway.GetActorId().GetLongId()];  // await gateway.GetLastUpdatedAsync();

                        if (!lastWritten.Equals(updated))
                        {
                            lastWritten = updated;
                            await WriteConfigAsync(cancellationToken);

                            LaunchNginxProcess($"-c \"{Path.GetFullPath("nginx.conf")}\" -s reload");
                        }

                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogWarning(new EventId(), ex, "RunAsync Failed");
                throw;
            }




        }
        #endregion StatelessService


    }
}
