﻿using Microsoft.AspNetCore.Mvc;
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
    [Route("api/notify")]
    [ApiController]
    public class NotifyController : ControllerBase
    {
        private readonly IRestClientService _restClientService;
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly string _appId;
        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;

        public NotifyController(IRestClientService restClientService, IBotFrameworkHttpAdapter adapter, IConfiguration configuration, ConcurrentDictionary<string, ConversationReference> conversationReferences)
        {
            _restClientService = restClientService;
            _adapter = adapter;
            _conversationReferences = conversationReferences;
            _appId = configuration["MicrosoftAppId"] ?? string.Empty;
        }
        
        public async Task<IActionResult> Get()
        {
            foreach (var conversationReference in _conversationReferences.Values)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, BotCallback, default(CancellationToken));
            }
            return Ok();
        }

        private async Task BotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var APIBaseUrl = "https://delivery-3rd-test-api.azurewebsites.net";
            var RiderId = "637937263065127099";
            var riderDetailsApi = $"{APIBaseUrl}/api/Rider/GetRiderInfo/{RiderId}";
            var request = await _restClientService.Get<EmployeeDetails>(riderDetailsApi);

            if (request.OrderRequest == null)
            {
                var message = "ระบบส่ง noti แต่ไม่มีออเดอร์";
                var text = MessageFactory.Text(message);
                await turnContext.SendActivityAsync(text);
                return;
            }

            var card = new HeroCard
            {
                Title = $"ออเดอร์ {request.OrderRequest.OrderCode}",
                Subtitle = $"ร้าน {request.OrderRequest.Restaurant.Name}{Environment.NewLine}{request.OrderRequest.Restaurant.Address}{Environment.NewLine}ผู้รับ {request.OrderRequest.Customer.Name}{Environment.NewLine}{request.OrderRequest.Customer.Address}{Environment.NewLine}{request.OrderRequest.Customer.Remark}",
                Text = "รับงานภายใน 30 วินาที",
                Buttons = new List<CardAction> {
                    new(ActionTypes.ImBack, title: "รับออเดอร์", value: $"รับออเดอร์ {request.OrderRequest._id}")
                }
            };
            var attachment = card.ToAttachment();
            var reply = MessageFactory.Attachment(attachment);
            await turnContext.SendActivityAsync(reply, cancellationToken);
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
