using Flurl.Http;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Playground.Services
{
    public interface IRestClientService
    {
        Task<T> Get<T>(string endpointUrl, object headers);
        Task<T> Post<T>(string endpointUrl, string requestBody, object headers);
        Task<T> Put<T>(string endpointUrl, string requestBody, object headers);
    }
    public class RestClientService : IRestClientService
    {
        public async Task<T> Get<T>(string endpointUrl, object headers)
        {
            var rsp = await endpointUrl
                .WithHeaders(headers)
                .GetAsync();
            return await rsp.ResponseMessage.Content.ReadFromJsonAsync<T>();
        }

        public async Task<T> Post<T>(string endpointUrl, string requestBody, object headers)
        {
            var rsp = await endpointUrl
                .WithHeaders(headers)
                .PostAsync(new StringContent(requestBody, Encoding.UTF8, "application/json"));
            return await rsp.ResponseMessage.Content.ReadFromJsonAsync<T>();
        }

        public async Task<T> Put<T>(string endpointUrl, string requestBody, object headers)
        {
            var rsp = await endpointUrl
                .WithHeaders(headers)
                .PutAsync(new StringContent(requestBody, Encoding.UTF8, "application/json"));
            return await rsp.ResponseMessage.Content.ReadFromJsonAsync<T>();
        }
    }
}
