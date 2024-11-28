using Flurl.Http;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Playground.Services
{
    public interface IRestClientService
    {
        Task<T> Get<T>(string endpointUrl, string userId);
        Task<T> Post<T>(string endpointUrl, string userId, string requestBody);
        Task Put(string endpointUrl, string userId, string requestBody = null);
        Task<T> Put<T>(string endpointUrl, string userId, string requestBody);
    }
    public class RestClientService : IRestClientService
    {
        public async Task<T> Get<T>(string endpointUrl, string userId)
        {
            try
            {
                var headers = new { Line_id = userId };
                var rsp = await endpointUrl
                    .WithHeaders(headers)
                    .GetAsync();
                if (rsp.StatusCode == 200)
                    return await rsp.ResponseMessage.Content.ReadFromJsonAsync<T>();
            }
            catch { }
            return default;
        }

        public async Task<T> Post<T>(string endpointUrl, string userId, string requestBody)
        {
            try
            {
                var headers = new { Line_id = userId };
                var rsp = await endpointUrl
                    .WithHeaders(headers)
                    .PostAsync(new StringContent(requestBody, Encoding.UTF8, "application/json"));
                if (rsp.StatusCode == 200)
                    return await rsp.ResponseMessage.Content.ReadFromJsonAsync<T>();
            }
            catch { }
            return default;
        }

        public async Task Put(string endpointUrl, string userId, string requestBody = null)
        {
            try
            {
                var headers = new { Line_id = userId };
                await endpointUrl
                    .WithHeaders(headers)
                    .PutAsync(new StringContent(requestBody, Encoding.UTF8, "application/json"));
            }
            catch { }
        }

        public async Task<T> Put<T>(string endpointUrl, string userId, string requestBody)
        {
            try
            {
                var headers = new { Line_id = userId };
                var rsp = await endpointUrl
                    .WithHeaders(headers)
                    .PutAsync(new StringContent(requestBody, Encoding.UTF8, "application/json"));
                if (rsp.StatusCode == 200)
                    return await rsp.ResponseMessage.Content.ReadFromJsonAsync<T>();
            }
            catch { }
            return default;
        }
    }
}
