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

namespace SInnovations.ServiceFabric.GatewayService
{
    public class Program
    {
        // Entry point for the application.
        public static void Main(string[] args)
        {
            ServiceRuntime.RegisterServiceAsync("GatewayServiceType", context => new WebHostingService(context, "ServiceEndpoint")).GetAwaiter().GetResult();

            Thread.Sleep(Timeout.Infinite);
        }

        /// <summary>
        /// A specialized stateless service for hosting ASP.NET Core web apps.
        /// </summary>
        internal sealed class WebHostingService : StatelessService, ICommunicationListener
        {

            public static readonly Dictionary<string, string> ExtensionMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase){
                {".gpx","application/gpx+xml"},
                {".axyz","application/ascend-xyz"},
                {".json","application/json"},
                {".geojson","application/vnd.geo+json"},
                { ".323", "text/h323" },
                { ".3g2", "video/3gpp2" },
                { ".3gp2", "video/3gpp2" },
                { ".3gp", "video/3gpp" },
                { ".3gpp", "video/3gpp" },
                { ".aac", "audio/aac" },
                { ".aaf", "application/octet-stream" },
                { ".aca", "application/octet-stream" },
                { ".accdb", "application/msaccess" },
                { ".accde", "application/msaccess" },
                { ".accdt", "application/msaccess" },
                { ".acx", "application/internet-property-stream" },
                { ".adt", "audio/vnd.dlna.adts" },
                { ".adts", "audio/vnd.dlna.adts" },
                { ".afm", "application/octet-stream" },
                { ".ai", "application/postscript" },
                { ".aif", "audio/x-aiff" },
                { ".aifc", "audio/aiff" },
                { ".aiff", "audio/aiff" },
                { ".application", "application/x-ms-application" },
                { ".art", "image/x-jg" },
                { ".asd", "application/octet-stream" },
                { ".asf", "video/x-ms-asf" },
                { ".asi", "application/octet-stream" },
                { ".asm", "text/plain" },
                { ".asr", "video/x-ms-asf" },
                { ".asx", "video/x-ms-asf" },
                { ".atom", "application/atom+xml" },
                { ".au", "audio/basic" },
                { ".avi", "video/x-msvideo" },
                { ".axs", "application/olescript" },
                { ".bas", "text/plain" },
                { ".bcpio", "application/x-bcpio" },
                { ".bin", "application/octet-stream" },
                { ".bmp", "image/bmp" },
                { ".c", "text/plain" },
                { ".cab", "application/vnd.ms-cab-compressed" },
                { ".calx", "application/vnd.ms-office.calx" },
                { ".cat", "application/vnd.ms-pki.seccat" },
                { ".cdf", "application/x-cdf" },
                { ".chm", "application/octet-stream" },
                { ".class", "application/x-java-applet" },
                { ".clp", "application/x-msclip" },
                { ".cmx", "image/x-cmx" },
                { ".cnf", "text/plain" },
                { ".cod", "image/cis-cod" },
                { ".cpio", "application/x-cpio" },
                { ".cpp", "text/plain" },
                { ".crd", "application/x-mscardfile" },
                { ".crl", "application/pkix-crl" },
                { ".crt", "application/x-x509-ca-cert" },
                { ".csh", "application/x-csh" },
                { ".css", "text/css" },
                { ".csv", "application/octet-stream" },
                { ".cur", "application/octet-stream" },
                { ".dcr", "application/x-director" },
                { ".deploy", "application/octet-stream" },
                { ".der", "application/x-x509-ca-cert" },
                { ".dib", "image/bmp" },
                { ".dir", "application/x-director" },
                { ".disco", "text/xml" },
                { ".dlm", "text/dlm" },
                { ".doc", "application/msword" },
                { ".docm", "application/vnd.ms-word.document.macroEnabled.12" },
                { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
                { ".dot", "application/msword" },
                { ".dotm", "application/vnd.ms-word.template.macroEnabled.12" },
                { ".dotx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template" },
                { ".dsp", "application/octet-stream" },
                { ".dtd", "text/xml" },
                { ".dvi", "application/x-dvi" },
                { ".dvr-ms", "video/x-ms-dvr" },
                { ".dwf", "drawing/x-dwf" },
                { ".dwp", "application/octet-stream" },
                { ".dxr", "application/x-director" },
                { ".eml", "message/rfc822" },
                { ".emz", "application/octet-stream" },
                { ".eot", "application/vnd.ms-fontobject" },
                { ".eps", "application/postscript" },
                { ".etx", "text/x-setext" },
                { ".evy", "application/envoy" },
                { ".fdf", "application/vnd.fdf" },
                { ".fif", "application/fractals" },
                { ".fla", "application/octet-stream" },
                { ".flr", "x-world/x-vrml" },
                { ".flv", "video/x-flv" },
                { ".gif", "image/gif" },
                { ".gtar", "application/x-gtar" },
                { ".gz", "application/x-gzip" },
                { ".h", "text/plain" },
                { ".hdf", "application/x-hdf" },
                { ".hdml", "text/x-hdml" },
                { ".hhc", "application/x-oleobject" },
                { ".hhk", "application/octet-stream" },
                { ".hhp", "application/octet-stream" },
                { ".hlp", "application/winhlp" },
                { ".hqx", "application/mac-binhex40" },
                { ".hta", "application/hta" },
                { ".htc", "text/x-component" },
                { ".htm", "text/html" },
                { ".html", "text/html" },
                { ".htt", "text/webviewhtml" },
                { ".hxt", "text/html" },
                { ".ical", "text/calendar" },
                { ".icalendar", "text/calendar" },
                { ".ico", "image/x-icon" },
                { ".ics", "text/calendar" },
                { ".ief", "image/ief" },
                { ".ifb", "text/calendar" },
                { ".iii", "application/x-iphone" },
                { ".inf", "application/octet-stream" },
                { ".ins", "application/x-internet-signup" },
                { ".isp", "application/x-internet-signup" },
                { ".IVF", "video/x-ivf" },
                { ".jar", "application/java-archive" },
                { ".java", "application/octet-stream" },
                { ".jck", "application/liquidmotion" },
                { ".jcz", "application/liquidmotion" },
                { ".jfif", "image/pjpeg" },
                { ".jpb", "application/octet-stream" },
                { ".jpe", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".jpg", "image/jpeg" },
                { ".js", "application/javascript" },
                { ".jsx", "text/jscript" },
                { ".latex", "application/x-latex" },
                { ".lit", "application/x-ms-reader" },
                { ".lpk", "application/octet-stream" },
                { ".lsf", "video/x-la-asf" },
                { ".lsx", "video/x-la-asf" },
                { ".lzh", "application/octet-stream" },
                { ".m13", "application/x-msmediaview" },
                { ".m14", "application/x-msmediaview" },
                { ".m1v", "video/mpeg" },
                { ".m2ts", "video/vnd.dlna.mpeg-tts" },
                { ".m3u", "audio/x-mpegurl" },
                { ".m4a", "audio/mp4" },
                { ".m4v", "video/mp4" },
                { ".man", "application/x-troff-man" },
                { ".manifest", "application/x-ms-manifest" },
                { ".map", "text/plain" },
                { ".mdb", "application/x-msaccess" },
                { ".mdp", "application/octet-stream" },
                { ".me", "application/x-troff-me" },
                { ".mht", "message/rfc822" },
                { ".mhtml", "message/rfc822" },
                { ".mid", "audio/mid" },
                { ".midi", "audio/mid" },
                { ".mix", "application/octet-stream" },
                { ".mmf", "application/x-smaf" },
                { ".mno", "text/xml" },
                { ".mny", "application/x-msmoney" },
                { ".mov", "video/quicktime" },
                { ".movie", "video/x-sgi-movie" },
                { ".mp2", "video/mpeg" },
                { ".mp3", "audio/mpeg" },
                { ".mp4", "video/mp4" },
                { ".mp4v", "video/mp4" },
                { ".mpa", "video/mpeg" },
                { ".mpe", "video/mpeg" },
                { ".mpeg", "video/mpeg" },
                { ".mpg", "video/mpeg" },
                { ".mpp", "application/vnd.ms-project" },
                { ".mpv2", "video/mpeg" },
                { ".ms", "application/x-troff-ms" },
                { ".msi", "application/octet-stream" },
                { ".mso", "application/octet-stream" },
                { ".mvb", "application/x-msmediaview" },
                { ".mvc", "application/x-miva-compiled" },
                { ".nc", "application/x-netcdf" },
                { ".nsc", "video/x-ms-asf" },
                { ".nws", "message/rfc822" },
                { ".ocx", "application/octet-stream" },
                { ".oda", "application/oda" },
                { ".odc", "text/x-ms-odc" },
                { ".ods", "application/oleobject" },
                { ".oga", "audio/ogg" },
                { ".ogg", "video/ogg" },
                { ".ogv", "video/ogg" },
                { ".ogx", "application/ogg" },
                { ".one", "application/onenote" },
                { ".onea", "application/onenote" },
                { ".onetoc", "application/onenote" },
                { ".onetoc2", "application/onenote" },
                { ".onetmp", "application/onenote" },
                { ".onepkg", "application/onenote" },
                { ".osdx", "application/opensearchdescription+xml" },
                { ".otf", "font/otf" },
                { ".p10", "application/pkcs10" },
                { ".p12", "application/x-pkcs12" },
                { ".p7b", "application/x-pkcs7-certificates" },
                { ".p7c", "application/pkcs7-mime" },
                { ".p7m", "application/pkcs7-mime" },
                { ".p7r", "application/x-pkcs7-certreqresp" },
                { ".p7s", "application/pkcs7-signature" },
                { ".pbm", "image/x-portable-bitmap" },
                { ".pcx", "application/octet-stream" },
                { ".pcz", "application/octet-stream" },
                { ".pdf", "application/pdf" },
                { ".pfb", "application/octet-stream" },
                { ".pfm", "application/octet-stream" },
                { ".pfx", "application/x-pkcs12" },
                { ".pgm", "image/x-portable-graymap" },
                { ".pko", "application/vnd.ms-pki.pko" },
                { ".pma", "application/x-perfmon" },
                { ".pmc", "application/x-perfmon" },
                { ".pml", "application/x-perfmon" },
                { ".pmr", "application/x-perfmon" },
                { ".pmw", "application/x-perfmon" },
                { ".png", "image/png" },
                { ".pnm", "image/x-portable-anymap" },
                { ".pnz", "image/png" },
                { ".pot", "application/vnd.ms-powerpoint" },
                { ".potm", "application/vnd.ms-powerpoint.template.macroEnabled.12" },
                { ".potx", "application/vnd.openxmlformats-officedocument.presentationml.template" },
                { ".ppam", "application/vnd.ms-powerpoint.addin.macroEnabled.12" },
                { ".ppm", "image/x-portable-pixmap" },
                { ".pps", "application/vnd.ms-powerpoint" },
                { ".ppsm", "application/vnd.ms-powerpoint.slideshow.macroEnabled.12" },
                { ".ppsx", "application/vnd.openxmlformats-officedocument.presentationml.slideshow" },
                { ".ppt", "application/vnd.ms-powerpoint" },
                { ".pptm", "application/vnd.ms-powerpoint.presentation.macroEnabled.12" },
                { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
                { ".prf", "application/pics-rules" },
                { ".prm", "application/octet-stream" },
                { ".prx", "application/octet-stream" },
                { ".ps", "application/postscript" },
                { ".psd", "application/octet-stream" },
                { ".psm", "application/octet-stream" },
                { ".psp", "application/octet-stream" },
                { ".pub", "application/x-mspublisher" },
                { ".qt", "video/quicktime" },
                { ".qtl", "application/x-quicktimeplayer" },
                { ".qxd", "application/octet-stream" },
                { ".ra", "audio/x-pn-realaudio" },
                { ".ram", "audio/x-pn-realaudio" },
                { ".rar", "application/octet-stream" },
                { ".ras", "image/x-cmu-raster" },
                { ".rf", "image/vnd.rn-realflash" },
                { ".rgb", "image/x-rgb" },
                { ".rm", "application/vnd.rn-realmedia" },
                { ".rmi", "audio/mid" },
                { ".roff", "application/x-troff" },
                { ".rpm", "audio/x-pn-realaudio-plugin" },
                { ".rtf", "application/rtf" },
                { ".rtx", "text/richtext" },
                { ".scd", "application/x-msschedule" },
                { ".sct", "text/scriptlet" },
                { ".sea", "application/octet-stream" },
                { ".setpay", "application/set-payment-initiation" },
                { ".setreg", "application/set-registration-initiation" },
                { ".sgml", "text/sgml" },
                { ".sh", "application/x-sh" },
                { ".shar", "application/x-shar" },
                { ".sit", "application/x-stuffit" },
                { ".sldm", "application/vnd.ms-powerpoint.slide.macroEnabled.12" },
                { ".sldx", "application/vnd.openxmlformats-officedocument.presentationml.slide" },
                { ".smd", "audio/x-smd" },
                { ".smi", "application/octet-stream" },
                { ".smx", "audio/x-smd" },
                { ".smz", "audio/x-smd" },
                { ".snd", "audio/basic" },
                { ".snp", "application/octet-stream" },
                { ".spc", "application/x-pkcs7-certificates" },
                { ".spl", "application/futuresplash" },
                { ".spx", "audio/ogg" },
                { ".src", "application/x-wais-source" },
                { ".ssm", "application/streamingmedia" },
                { ".sst", "application/vnd.ms-pki.certstore" },
                { ".stl", "application/vnd.ms-pki.stl" },
                { ".sv4cpio", "application/x-sv4cpio" },
                { ".sv4crc", "application/x-sv4crc" },
                { ".svg", "image/svg+xml" },
                { ".svgz", "image/svg+xml" },
                { ".swf", "application/x-shockwave-flash" },
                { ".t", "application/x-troff" },
                { ".tar", "application/x-tar" },
                { ".tcl", "application/x-tcl" },
                { ".tex", "application/x-tex" },
                { ".texi", "application/x-texinfo" },
                { ".texinfo", "application/x-texinfo" },
                { ".tgz", "application/x-compressed" },
                { ".thmx", "application/vnd.ms-officetheme" },
                { ".thn", "application/octet-stream" },
                { ".tif", "image/tiff" },
                { ".tiff", "image/tiff" },
                { ".ecw","image/ecw"},
                { ".toc", "application/octet-stream" },
                { ".tr", "application/x-troff" },
                { ".trm", "application/x-msterminal" },
                { ".ts", "video/vnd.dlna.mpeg-tts" },
                { ".tsv", "text/tab-separated-values" },
                { ".ttf", "application/octet-stream" },
                { ".tts", "video/vnd.dlna.mpeg-tts" },
                { ".txt", "text/plain" },
                { ".u32", "application/octet-stream" },
                { ".uls", "text/iuls" },
                { ".ustar", "application/x-ustar" },
                { ".vbs", "text/vbscript" },
                { ".vcf", "text/x-vcard" },
                { ".vcs", "text/plain" },
                { ".vdx", "application/vnd.ms-visio.viewer" },
                { ".vml", "text/xml" },
                { ".vsd", "application/vnd.visio" },
                { ".vss", "application/vnd.visio" },
                { ".vst", "application/vnd.visio" },
                { ".vsto", "application/x-ms-vsto" },
                { ".vsw", "application/vnd.visio" },
                { ".vsx", "application/vnd.visio" },
                { ".vtx", "application/vnd.visio" },
                { ".wav", "audio/wav" },
                { ".wax", "audio/x-ms-wax" },
                { ".wbmp", "image/vnd.wap.wbmp" },
                { ".wcm", "application/vnd.ms-works" },
                { ".wdb", "application/vnd.ms-works" },
                { ".webm", "video/webm" },
                { ".wks", "application/vnd.ms-works" },
                { ".wm", "video/x-ms-wm" },
                { ".wma", "audio/x-ms-wma" },
                { ".wmd", "application/x-ms-wmd" },
                { ".wmf", "application/x-msmetafile" },
                { ".wml", "text/vnd.wap.wml" },
                { ".wmlc", "application/vnd.wap.wmlc" },
                { ".wmls", "text/vnd.wap.wmlscript" },
                { ".wmlsc", "application/vnd.wap.wmlscriptc" },
                { ".wmp", "video/x-ms-wmp" },
                { ".wmv", "video/x-ms-wmv" },
                { ".wmx", "video/x-ms-wmx" },
                { ".wmz", "application/x-ms-wmz" },
                { ".woff", "application/font-woff" },
                { ".woff2", "application/font-woff2" },
                { ".wps", "application/vnd.ms-works" },
                { ".wri", "application/x-mswrite" },
                { ".wrl", "x-world/x-vrml" },
                { ".wrz", "x-world/x-vrml" },
                { ".wsdl", "text/xml" },
                { ".wtv", "video/x-ms-wtv" },
                { ".wvx", "video/x-ms-wvx" },
                { ".x", "application/directx" },
                { ".xaf", "x-world/x-vrml" },
                { ".xaml", "application/xaml+xml" },
                { ".xap", "application/x-silverlight-app" },
                { ".xbap", "application/x-ms-xbap" },
                { ".xbm", "image/x-xbitmap" },
                { ".xdr", "text/plain" },
                { ".xht", "application/xhtml+xml" },
                { ".xhtml", "application/xhtml+xml" },
                { ".xla", "application/vnd.ms-excel" },
                { ".xlam", "application/vnd.ms-excel.addin.macroEnabled.12" },
                { ".xlc", "application/vnd.ms-excel" },
                { ".xlm", "application/vnd.ms-excel" },
                { ".xls", "application/vnd.ms-excel" },
                { ".xlsb", "application/vnd.ms-excel.sheet.binary.macroEnabled.12" },
                { ".xlsm", "application/vnd.ms-excel.sheet.macroEnabled.12" },
                { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                { ".xlt", "application/vnd.ms-excel" },
                { ".xltm", "application/vnd.ms-excel.template.macroEnabled.12" },
                { ".xltx", "application/vnd.openxmlformats-officedocument.spreadsheetml.template" },
                { ".xlw", "application/vnd.ms-excel" },
                { ".xml", "text/xml" },
                { ".xof", "x-world/x-vrml" },
                { ".xpm", "image/x-xpixmap" },
                { ".xps", "application/vnd.ms-xpsdocument" },
                { ".xsd", "text/xml" },
                { ".xsf", "text/xml" },
                { ".xsl", "text/xml" },
                { ".xslt", "text/xml" },
                { ".xsn", "application/octet-stream" },
                { ".xtp", "application/octet-stream" },
                { ".xwd", "image/x-xwindowdump" },
                { ".z", "application/x-compress" },
                { ".zip", "application/x-zip-compressed" },
                {".vrt","image/virtual-raster"}
        };

            private readonly string _endpointName;
            private string nginxProcessName = "";

            private IWebHost _webHost;

            public WebHostingService(StatelessServiceContext serviceContext, string endpointName)
                : base(serviceContext)
            {
                _endpointName = endpointName;
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
                var endpoint = FabricRuntime.GetActivationContext().GetEndpoint(_endpointName);
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
                foreach(var type in ExtensionMapping.GroupBy(kv => kv.Value,kv=>kv.Key))
                {
                    mime.AppendLine($"\t{type.Key} {string.Join(" ", type.Select(t=>t.Trim('.')))};");
                }
                mime.AppendLine("}");

                return mime;

            }

            private static void WriteProxyPassLocation(int level, string pathPrefix,string url, StringBuilder sb)
            {
            
                var tabs = string.Join("", Enumerable.Range(0, level+1).Select(r => "\t"));
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

            Task<string> ICommunicationListener.OpenAsync(CancellationToken cancellationToken)
            {
                
                var endpoint = FabricRuntime.GetActivationContext().GetEndpoint("ServiceEndpoint1");

                string serverUrl = $"{endpoint.Protocol}://{FabricRuntime.GetNodeContext().IPAddressOrFQDN}:{endpoint.Port}";

                _webHost = new WebHostBuilder().UseKestrel()
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
}
