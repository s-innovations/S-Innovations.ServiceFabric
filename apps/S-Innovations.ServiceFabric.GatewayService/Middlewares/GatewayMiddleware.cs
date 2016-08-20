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
        public HttpGatewayMiddleware(RequestDelegate next, HttpCommunicationClientFactory httpClientFactory)
        {
            if (httpClientFactory == null)
            {
                throw new ArgumentNullException(nameof(httpClientFactory));
            }

            _httpClientFactory = httpClientFactory;

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
               

                var servicePartitionClient = new ServicePartitionClient<HttpCommunicationClient>(_httpClientFactory,
                                                                                               _options.ServiceUri,
                                                                                               _options.GetServicePartitionKey?.Invoke(context),
                                                                                               _options.TargetReplicaSelector,
                                                                                               _options.ListenerName,
                                                                                               _options.OperationRetrySettings);

                using (var responseMessage = await servicePartitionClient.InvokeWithRetryAsync(httpClient => ExecuteServiceCallAsync(httpClient, context,_options)))
                {
                    await responseMessage.CopyToCurrentContext(context);
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
            if (options.StickySession && !string.IsNullOrEmpty(value)){
                baseAddress = new Uri( value);
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
