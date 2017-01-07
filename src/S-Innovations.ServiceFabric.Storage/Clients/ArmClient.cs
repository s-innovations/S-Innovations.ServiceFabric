using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SInnovations.ServiceFabric.Storage.Extensions;

namespace SInnovations.ServiceFabric.Storage.Clients
{
    public class ArmClient
    {


        protected HttpClient Client { get; set; }
        public ArmClient(AuthenticationHeaderValue authorization)
        {
            Client = new HttpClient();
            Client.DefaultRequestHeaders.Authorization = authorization;
        }

        public ArmClient(string accessToken) : this(new AuthenticationHeaderValue("bearer", accessToken))
        {

        }

        public Task<T> ListKeysAsync<T>(string resourceId, string apiVersion)
        {
            var resourceUrl = $"https://management.azure.com/{resourceId.Trim('/')}/listkeys?api-version={apiVersion}";

            return Client.PostAsync(resourceUrl, new StringContent(string.Empty))
                .As<T>();
        }

        public Task<T> PatchAsync<T>(string resourceId, T value, string apiVersion)
        {
            var resourceUrl = $"https://management.azure.com/{resourceId.Trim('/')}?api-version={apiVersion}";
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), resourceUrl);
            var valuestr = JsonConvert.SerializeObject(value);
            request.Content = new StringContent(valuestr, Encoding.UTF8, "application/json");

            return Client.SendAsync(request)
                .As<T>();
        }

        public Task<T> GetAsync<T>(string resourceId, string apiVersion)
        {
            var resourceUrl = $"https://management.azure.com/{resourceId.Trim('/')}?api-version={apiVersion}";
            return Client.GetAsync(resourceUrl).As<T>();
        }
    }
}
