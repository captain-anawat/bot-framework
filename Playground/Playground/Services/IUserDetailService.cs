using Playground.Models;
using System.Threading.Tasks;

namespace Playground.Services
{
    public interface IUserDetailService
    {
        Task<UserDetails> TryGetUserDetail(UserDetails userDetails, string userId);
        Task<EmployeeDetails> GetOrderRequest(UserDetails userDetails, string userId);
    }
    public class UserDetailService : IUserDetailService
    {
        private readonly IRestClientService _restClientService;
        private readonly ConnectionSettings _connectionSettings;

        public UserDetailService(IRestClientService restClientService, ConnectionSettings connectionSettings)
        {
            _restClientService = restClientService;
            _connectionSettings = connectionSettings;
        }
        public async Task<UserDetails> TryGetUserDetail(UserDetails userDetails, string userId)
        {
            var apiStr = $"{_connectionSettings.DeliveryAPIBaseUrl}/api/Rider/GetRiderInfoWithChatBotId";
            var info = await _restClientService.Get<EmployeeDetails>(apiStr, userId);
            if (info is null) return userDetails;

            userDetails.IsLinkedAccount = true;
            userDetails.RiderId = info._id;
            userDetails.UserName = info.Name;
            userDetails.DeliveryName = info.DeliveryName;
            userDetails.PhoneNumber = info.PhoneNumber;
            userDetails.WorkStatus = info.OnWorkStatus;
            apiStr = $"{_connectionSettings.DeliveryAPIBaseUrl}/api/Rider/GetUnfinishedOrder/{info._id}";
            var order = await _restClientService.Get<OrderResponse>(apiStr, userId);
            userDetails.UnfinishOrder = order is not null ? order._id : null;
            return userDetails;
        }

        public async Task<EmployeeDetails> GetOrderRequest(UserDetails userDetails, string userId)
        {
            var apiStr = $"{_connectionSettings.DeliveryAPIBaseUrl}/api/Rider/GetRiderInfoWithChatBotId";
            var info = await _restClientService.Get<EmployeeDetails>(apiStr, userId);
            return info;
        }
    }
}
