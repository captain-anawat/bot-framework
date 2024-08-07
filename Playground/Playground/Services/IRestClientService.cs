using Flurl.Http;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Playground.Services
{
    public interface IRestClientService
    {
        Task<T> Get<T>(string endpointUrl);
        Task<T> Post<T>(string endpointUrl, string requestBody);
        Task Put(string endpointUrl, string requestBody);
        Task<T> Put<T>(string endpointUrl, string requestBody);
    }
    public class RestClientService : IRestClientService
    {
        public async Task<T> Get<T>(string endpointUrl)
        {
            var rsp = await endpointUrl
                .GetAsync();
            if (rsp.StatusCode == 200)
                return await rsp.ResponseMessage.Content.ReadFromJsonAsync<T>();

            return default(T);
        }

        public async Task<T> Post<T>(string endpointUrl, string requestBody)
        {
            var rsp = await endpointUrl
                .PostAsync(new StringContent(requestBody, Encoding.UTF8, "application/json"));
            if (rsp.StatusCode == 200)
                return await rsp.ResponseMessage.Content.ReadFromJsonAsync<T>();

            return default(T);
        }

        public async Task Put(string endpointUrl, string requestBody)
        {
            await endpointUrl.PutAsync(new StringContent(requestBody, Encoding.UTF8, "application/json"));
        }

        public async Task<T> Put<T>(string endpointUrl, string requestBody)
        {
            var rsp = await endpointUrl
                .PutAsync(new StringContent(requestBody, Encoding.UTF8, "application/json"));
            if (rsp.StatusCode == 200)
                return await rsp.ResponseMessage.Content.ReadFromJsonAsync<T>();

            return default(T);
        }
    }
}
