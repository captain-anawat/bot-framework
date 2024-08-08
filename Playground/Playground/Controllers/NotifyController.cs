using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Playground.Dialogs;
using Playground.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Playground.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class NotifyController : ControllerBase
    {
        private readonly string APIBaseUrl = "https://delivery-3rd-test-api.azurewebsites.net";
        private readonly IBotStateService _botStateService;
        private readonly IRestClientService _restClientService;
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly string _appId;
        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;

        public NotifyController(IBotStateService botStateService, IRestClientService restClientService, IBotFrameworkHttpAdapter adapter, IConfiguration configuration, ConcurrentDictionary<string, ConversationReference> conversationReferences)
        {
            _botStateService = botStateService;
            _restClientService = restClientService;
            _adapter = adapter;
            _conversationReferences = conversationReferences;
            _appId = configuration["MicrosoftAppId"] ?? string.Empty;
        }

        [HttpGet("{riderId}")]
        public async Task<IActionResult> Ordering(string riderId)
        {
            foreach (var conversationReference in _conversationReferences.Values)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, RequestBotCallback, default(CancellationToken));
            }
            return Ok();

            async Task RequestBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);
                if (userDetails.RiderId != riderId) return;
                var riderDetailsApi = $"{APIBaseUrl}/api/Rider/GetRiderInfo/{userDetails.RiderId}";
                var request = await _restClientService.Get<EmployeeDetails>(riderDetailsApi);
                if (request.OrderRequest == null)
                    return;

                userDetails.RequestOrder = request.OrderRequest._id;
                await _botStateService.SaveChangesAsync(turnContext);
                var card = new HeroCard
                {
                    Title = $"ออเดอร์ {request.OrderRequest.OrderCode}",
                    Subtitle = $"ร้าน {request.OrderRequest.Restaurant.Name}{Environment.NewLine}{request.OrderRequest.Restaurant.Address}{Environment.NewLine}ผู้รับ {request.OrderRequest.Customer.Name}{Environment.NewLine}{request.OrderRequest.Customer.Address}{Environment.NewLine}{request.OrderRequest.Customer.Remark}",
                    Text = "รับงานภายใน 30 วินาที",
                    Buttons = new List<CardAction> {
                        new(ActionTypes.ImBack, title: "รับออเดอร์", value: $"รับออเดอร์")
                    }
                };
                var attachment = card.ToAttachment();
                var reply = MessageFactory.Attachment(attachment);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }

        [HttpGet("{riderId}")]
        public async Task<IActionResult> Timeup(string riderId)
        {
            foreach (var conversationReference in _conversationReferences.Values)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, TimeupBotCallback, default(CancellationToken));
            }
            return Ok();

            async Task TimeupBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);
                if (userDetails.RiderId != riderId) return;

                userDetails.RequestOrder = string.Empty;
                await _botStateService.SaveChangesAsync(turnContext);

                var messageText = "คุณกดไม่ทับเวลารับงาน กรุณารองานถัดไป";
                var reply = MessageFactory.Text(messageText, messageText);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }

        [HttpGet("{riderId}")]
        public async Task<IActionResult> Cancel(string riderId)
        {
            foreach (var conversationReference in _conversationReferences.Values)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, CancelBotCallback, default(CancellationToken));
            }
            return Ok();

            async Task CancelBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);
                if (userDetails.RiderId != riderId) return;

                userDetails.UnfinishOrder = string.Empty;
                await _botStateService.SaveChangesAsync(turnContext);

                var messageText = "คำขอยกเลิกออเดอร์ได้รับการอนุมัติ";
                var reply = MessageFactory.Text(messageText, messageText);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }

        [HttpGet("{riderId}")]
        public async Task<IActionResult> CancelDeny(string riderId)
        {
            foreach (var conversationReference in _conversationReferences.Values)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, CancelDenyBotCallback, default(CancellationToken));
            }
            return Ok();

            async Task CancelDenyBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);
                if (userDetails.RiderId != riderId) return;
                var messageText = "คำขอยกเลิกออเดอร์ถูกปฎิเสธ";
                var reply = MessageFactory.Text(messageText, messageText);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }

        [HttpGet("{riderId}")]
        public async Task<IActionResult> Done(string riderId)
        {
            foreach (var conversationReference in _conversationReferences.Values)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, DoneBotCallback, default(CancellationToken));
            }
            return Ok();

            async Task DoneBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);
                if (userDetails.RiderId != riderId) return;

                userDetails.UnfinishOrder = string.Empty;
                await _botStateService.SaveChangesAsync(turnContext);
                var messageText = "คุณส่งออเดอร์เรียบร้อย";
                var reply = MessageFactory.Text(messageText, messageText);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }
    }

    public class OrderResponse
    {
        public string _id { get; set; }
        public string OrderCode { get; set; }
        public string ManaEndpoint { get; set; }
        public ContactInfo Restaurant { get; set; }
        public ContactInfo Customer { get; set; }
    }
    public class ContactInfo
    {
        public string _id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string Remark { get; set; }
    }
}
