using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using SInnovations.ServiceFabric.GatewayService.Communication;
using Microsoft.Extensions.DependencyInjection;
using SInnovations.ServiceFabric.GatewayService.Extensions;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace SInnovations.ServiceFabric.GatewayService.Middlewares
{

    public class SimpleHttpResponseException : Exception
    {
        public HttpStatusCode StatusCode { get; private set; }

        public SimpleHttpResponseException(HttpStatusCode statusCode, string content) : base(content)
        {
            StatusCode = statusCode;
        }
    }

    public class HttpGatewayMiddleware
    {
        private readonly HttpCommunicationClientFactory _httpClientFactory;
        private readonly WsCommunicationClientFactory _wsClientFactory;

        public HttpGatewayMiddleware(RequestDelegate next, HttpCommunicationClientFactory httpClientFactory, WsCommunicationClientFactory wsClientFactory)
        {
            if (httpClientFactory == null)
            {
                throw new ArgumentNullException(nameof(httpClientFactory));
            }
            if (wsClientFactory == null)
            {
                throw new ArgumentNullException(nameof(wsClientFactory));
            }

            _httpClientFactory = httpClientFactory;
            _wsClientFactory = wsClientFactory;

        }

        public async Task Invoke(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }


            try
            {

                var serviceProviderManager = context.RequestServices.GetService<HttpGatewayServiceManager>();
                var _options = serviceProviderManager.ResolveGatewayServiceInfomationAndUpdateRequestPath(context);


                if (context.WebSockets.IsWebSocketRequest)
                {
                    var resoler = ServicePartitionResolver.GetDefault();

                    var node = await resoler.ResolveAsync(_options.ServiceUri,
                                                           _options.GetServicePartitionKey?.Invoke(context),
                                                           CancellationToken.None);
                    var baseAddress = JObject.Parse(node.Endpoints.First().Address).SelectToken("Endpoints").Value<string>("");
                    // Sticky Sessions
                    string value;
                    context.Request.Cookies.TryGetValue("SERVER-SF", out value);
                    if (_options.StickySession && !string.IsNullOrEmpty(value))
                    {
                        baseAddress = value;
                    }

                    var wsClient = new WsCommunicationClient(baseAddress);
                    //Unsure if it gives anything to use the factory and retry stuff as we have to keep the connection open
                    //If exception happens, dont that imply we must close the connection to client also and client will initiate its retry logic.
                    
                    //var wsClient = await _wsClientFactory.GetClientAsync( node, 
                    //                                                      _options.TargetReplicaSelector,
                    //                                                      _options.ListenerName,
                    //                                                       _options.OperationRetrySettings,
                    //                                                       CancellationToken.None);



                    await wsClient.ConnectAsync(context, CancellationToken.None);
                    WebSocketReceiveResult result1 =null, result2=null;
                    
                    var buffer2 = new byte[1024 * 4];

                    result2 = await wsClient.clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer2), CancellationToken.None);

                    if (!result2.CloseStatus.HasValue)
                    {

                        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                        await webSocket.SendAsync(new ArraySegment<byte>(buffer2, 0, result2.Count), result2.MessageType, result2.EndOfMessage, CancellationToken.None);
                        

                        var buffer1 = new byte[1024 * 4];
                        var a = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer1), CancellationToken.None); ;
                        var b = wsClient.clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer2), CancellationToken.None);

                        while (!result2.CloseStatus.HasValue)
                        {
                            await Task.WhenAny(a, b);

                            if (a.IsCompleted)
                            {
                                result1 = await a;
                                if (!result1.CloseStatus.HasValue)
                                {
                                    await wsClient.clientWebSocket.SendAsync(new ArraySegment<byte>(buffer1, 0, result1.Count), result1.MessageType, result1.EndOfMessage, CancellationToken.None);
                                    a = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer1), CancellationToken.None);
                                }else
                                {
                                    await wsClient.clientWebSocket.CloseAsync(result1.CloseStatus.Value, result1.CloseStatusDescription, CancellationToken.None);
                                    await webSocket.CloseAsync(result1.CloseStatus.Value, result1.CloseStatusDescription, CancellationToken.None);
                                }
                            }

                            if (b.IsCompleted)
                            {
                                result2 = await b;
                                if (!result2.CloseStatus.HasValue)
                                {
                                    await webSocket.SendAsync(new ArraySegment<byte>(buffer2, 0, result2.Count), result2.MessageType, result2.EndOfMessage, CancellationToken.None);
                                    b = wsClient.clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer2), CancellationToken.None);
                                }
                            }

                        }
                        if (result2.CloseStatus.HasValue)
                        {
                            if (wsClient.clientWebSocket.State != WebSocketState.Closed)
                            {
                                await wsClient.clientWebSocket.CloseAsync(result2.CloseStatus.Value, result2.CloseStatusDescription, CancellationToken.None);
                            }
                            if (webSocket.State != WebSocketState.Closed)
                            {
                                await webSocket.CloseAsync(result2.CloseStatus.Value, result2.CloseStatusDescription, CancellationToken.None);
                            }

                        }





                    }
                }
                else
                {

                    var servicePartitionClient = new ServicePartitionClient<HttpCommunicationClient>(_httpClientFactory,
                                                                                                   _options.ServiceUri,
                                                                                                   _options.GetServicePartitionKey?.Invoke(context),
                                                                                                   _options.TargetReplicaSelector,
                                                                                                   _options.ListenerName,
                                                                                                   _options.OperationRetrySettings);

                    using (var responseMessage = await servicePartitionClient.InvokeWithRetryAsync(httpClient => ExecuteServiceCallAsync(httpClient, context, _options)))
                    {
                        await responseMessage.CopyToCurrentContext(context);
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }


        }

        private async Task<HttpResponseMessage> ExecuteServiceCallAsync(HttpCommunicationClient httpClient, HttpContext context, ServiceProviderInfomation options)
        {

            var requestMessage = new HttpRequestMessage();

            //
            // Copy the request method
            //
            requestMessage.Method = new HttpMethod(context.Request.Method);

            //
            // Copy the request content
            //
            if (!StringComparer.OrdinalIgnoreCase.Equals(context.Request.Method, "GET") &&
                    !StringComparer.OrdinalIgnoreCase.Equals(context.Request.Method, "HEAD") &&
                    !StringComparer.OrdinalIgnoreCase.Equals(context.Request.Method, "DELETE") &&
                    !StringComparer.OrdinalIgnoreCase.Equals(context.Request.Method, "TRACE"))
            {
                requestMessage.Content = new StreamContent(context.Request.Body);
            }


            requestMessage.CopyHeadersFromCurrentContext(context);
            requestMessage.AddProxyHeaders(context);

            //
            // Construct the request URL
            //
            var baseAddress = httpClient.BaseAddress;

            // Sticky Sessions
            string value;
            context.Request.Cookies.TryGetValue("SERVER-SF", out value);
            if (options.StickySession && !string.IsNullOrEmpty(value))
            {
                baseAddress = new Uri(value);
            }


            var pathAndQuery = PathString.FromUriComponent(baseAddress) + context.Request.Path + context.Request.QueryString;
            requestMessage.RequestUri = new Uri($"{baseAddress.Scheme}://{baseAddress.Host}:{baseAddress.Port}{pathAndQuery}", UriKind.Absolute);





            //
            // Send request and copy the result back to HttpResponse
            //
            var responseMessage = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

            //
            // If the service is temporarily unavailable, throw to retry later.
            //
            if (responseMessage.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                responseMessage.EnsureSuccessStatusCode();
            }

            // cases in which we want to invoke the retry logic from the ClientFactory
            int statusCode = (int)responseMessage.StatusCode;
            if ((statusCode >= 500 && statusCode < 600) || statusCode == (int)HttpStatusCode.NotFound)
            {
                throw new SimpleHttpResponseException(responseMessage.StatusCode, "Service call failed");
            }

            if (options.StickySession && string.IsNullOrEmpty(value))
            {
                context.Response.Cookies.Append("SERVER-SF", baseAddress.AbsoluteUri);
            }

            return responseMessage;

        }
    }
}
