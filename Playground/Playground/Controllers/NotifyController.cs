using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Playground.Models;
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

        [HttpGet("{riderId}/{botUserId}/{isApprove}")]
        public async Task<IActionResult> LinkAccount(string riderId, string botUserId, bool isApprove)
        {
            foreach (var conversationReference in _conversationReferences.Values)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, LinkAccountRequestBotCallback, default(CancellationToken));
            }
            return Ok();

            async Task LinkAccountRequestBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                if (turnContext.Activity.From.Id != botUserId) return;
                if (isApprove)
                {
                    var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);
                    userDetails.RiderId = riderId;
                    await _botStateService.SaveChangesAsync(turnContext);

                    var riderDetailsApi = $"{APIBaseUrl}/api/Rider/GetRiderInfo/{userDetails.RiderId}";
                    var info = await _restClientService.Get<EmployeeDetails>(riderDetailsApi);

                    var card = new HeroCard
                    {
                        Title = $"คุณ {info.Name} ได้ทำการผูก line account กับ mana เรียบร้อยแล้ว",
                        Buttons = new List<CardAction> {
                        new(ActionTypes.ImBack, title: "พร้อมเริ่มงาน", value: true)
                    }
                    };

                    var attachment = card.ToAttachment();
                    var reply = MessageFactory.Attachment(attachment);
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                }
                else
                {
                    var card = new HeroCard
                    {
                        Title = $"คุณถูกปฎิเสธการผูก line account กับ mana",
                        Buttons = new List<CardAction> {
                        new(ActionTypes.ImBack, title: "เริ่มผูกบัญชีใหม่", value: false)
                    }
                    };

                    var attachment = card.ToAttachment();
                    var reply = MessageFactory.Attachment(attachment);
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                }
            }
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

                var choices = new List<string> { "ปิด", "ติดต่อ" };
                var messageText = "คุณกดรับไม่ทันเวลาที่กำหนด กรุณารองานถัดไป";
                var reply = MessageFactory.SuggestedActions(choices, messageText, null, InputHints.ExpectingInput);
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

                var choices = new List<string> { "ปิด", "ติดต่อ" };
                var messageText = "คำขอยกเลิกออเดอร์ได้รับการอนุมัติ";
                var reply = MessageFactory.SuggestedActions(choices, messageText, null, InputHints.ExpectingInput);
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

                var choices = new List<string> { "ติดต่อ" };
                var messageText = "คำขอยกเลิกออเดอร์ถูกปฎิเสธ";
                var reply = MessageFactory.SuggestedActions(choices, messageText, null, InputHints.ExpectingInput);
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
                var choices = new List<string> { "ปิด", "ติดต่อ" };
                var messageText = "คุณส่งออเดอร์เรียบร้อย";
                var reply = MessageFactory.SuggestedActions(choices, messageText, null, InputHints.ExpectingInput);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }
    }
}
