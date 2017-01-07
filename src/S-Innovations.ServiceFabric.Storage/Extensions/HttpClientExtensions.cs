using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SInnovations.ServiceFabric.Storage.Clients.Arm;

namespace SInnovations.ServiceFabric.Storage.Extensions
{
    public static class HttpClientExtensions
    {


        public static async Task<T> As<T>(this Task<HttpResponseMessage> messageTask)
        {
            var message = await messageTask;

            if (!typeof(ArmErrorBase).IsAssignableFrom(typeof(T)) && !message.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await message.Content.ReadAsStringAsync());
            }

            using (var stream = await message.Content.ReadAsStreamAsync())
            {
                using (var sr = new JsonTextReader(new StreamReader(stream)))
                {
                    var serializer = JsonSerializer.Create(new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                    });

                    return serializer.Deserialize<T>(sr);
                }
            }
        }
    }
}
