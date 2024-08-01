using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Playground.Controllers
{
    [Route("api/notify")]
    [ApiController]
    public class NotifyController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly string _appId;
        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;

        public NotifyController(IBotFrameworkHttpAdapter adapter, IConfiguration configuration, ConcurrentDictionary<string, ConversationReference> conversationReferences)
        {
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
            var heroCard = new HeroCard
            {
                Title = "ออเดอร์ 12345",
                Subtitle = $"ร้าน xx{Environment.NewLine}xxxxx{Environment.NewLine}ผู้รับ xx{Environment.NewLine}xxxx",
                Text = "รับงานภายใน 30 วินาที",
                Buttons = new List<CardAction> {
                    new(ActionTypes.ImBack, title: "รับออเดอร์", value: "รับออเดอร์")
                }
            };
            var attachment = heroCard.ToAttachment();
            var reply = MessageFactory.Attachment(attachment);
            await turnContext.SendActivityAsync(reply, cancellationToken);
        }
    }
}
