﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Playground.Models;
using Playground.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Playground.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class NotifyController : ControllerBase
    {
        private readonly IBotStateService _botStateService;
        private readonly IRestClientService _restClientService;
        private readonly IUserDetailService _userDetailService;
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly IConversationReferenceRepository _referenceRepository;
        private readonly string _appId;
        private readonly ConnectionSettings _connectionSetting;
        private readonly IList<string> _standbyCmd = ["เปิด", "ปิด", "ติดต่อ"];

        public NotifyController(IBotStateService botStateService, IRestClientService restClientService, IUserDetailService userDetailService, IBotFrameworkHttpAdapter adapter, IConfiguration configuration, IConversationReferenceRepository referenceRepository, ConnectionSettings connectionSetting)
        {
            _botStateService = botStateService;
            _restClientService = restClientService;
            _userDetailService = userDetailService;
            _adapter = adapter;
            _referenceRepository = referenceRepository;
            _connectionSetting = connectionSetting;
            _appId = configuration["MicrosoftAppId"] ?? string.Empty;
        }

        [HttpGet("{botUserId}/{riderId}/{isApprove}")]
        public async Task<IActionResult> LinkAccount(string botUserId, string riderId, bool isApprove)
        {
            var conversationReference = await _referenceRepository.GetConversationReferenceAsync(botUserId);
            await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, LinkAccountRequestBotCallback, default(CancellationToken));
            return Ok();

            async Task LinkAccountRequestBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                if (turnContext.Activity.From.Id != botUserId) return;
                if (isApprove)
                {
                    var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);

                    userDetails.RiderId = riderId;
                    await _botStateService.SaveChangesAsync(turnContext);

                    if (riderId.StartsWith("mrid"))
                    {
                        userDetails.IsLinkedAccount = true;
                        userDetails.RiderId = riderId;
                        await _botStateService.SaveChangesAsync(turnContext);

                        var userName = turnContext.Activity.From.Name;
                        var card = new HeroCard
                        {
                            Title = $"คุณ {userName} ยังไม่ได้เข้าร่วมกับ delivery นี้",
                            Buttons = new List<CardAction> {
                                new(ActionTypes.ImBack, title: "ตกลง", value: "ตกลง")
                            }
                        };

                        var attachment = card.ToAttachment();
                        var reply = MessageFactory.Attachment(attachment);
                        await turnContext.SendActivityAsync(reply, cancellationToken);
                    }
                    else
                    {
                        var riderDetailsApi = $"{_connectionSetting.DeliveryAPIBaseUrl}/api/Rider/GetRiderInfo/{userDetails.RiderId}";
                        userDetails = await _userDetailService.TryGetUserDetail(userDetails, turnContext.Activity.From.Id);
                        await _botStateService.SaveChangesAsync(turnContext);

                        var card = new HeroCard
                        {
                            Title = $"คุณ {userDetails.UserName} ได้ทำการผูก line account กับ mana เรียบร้อยแล้ว",
                            Buttons = new List<CardAction> {
                                new(ActionTypes.ImBack, title: "พร้อมเริ่มงาน", value: "พร้อมเริ่มงาน")
                            }
                        };

                        var attachment = card.ToAttachment();
                        var reply = MessageFactory.Attachment(attachment);
                        await turnContext.SendActivityAsync(reply, cancellationToken);
                    }
                }
                else
                {
                    var card = new HeroCard
                    {
                        Title = $"คุณถูกปฎิเสธการผูก line account กับ mana",
                        Buttons = new List<CardAction> {
                            new(ActionTypes.ImBack, title: "เริ่มผูกบัญชีใหม่", value: "เริ่มผูกบัญชีใหม่")
                        }
                    };

                    var attachment = card.ToAttachment();
                    var reply = MessageFactory.Attachment(attachment);
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> Ordering(OrderingRequest request)
        {
            var invalid = request is null || request.ChatBotIds is null || request.ChatBotIds.Count is 0;
            if (invalid) return Ok();

            var conversationReferences = await _referenceRepository.ListConversationReferenceAsync(request.ChatBotIds);
            foreach (var conversationReference in conversationReferences)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, RequestBotCallback, default(CancellationToken));
            }
            return Ok();

            async Task RequestBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);

                if (userDetails is null || string.IsNullOrWhiteSpace(userDetails.RiderId))
                {
                    userDetails = await _userDetailService.TryGetUserDetail(userDetails, turnContext.Activity.From.Id);
                    await _botStateService.SaveChangesAsync(turnContext);
                }

                var request = await _userDetailService.GetOrderRequest(userDetails, turnContext.Activity.From.Id);
                if (request.OrderRequest == null) return;

                var card = new HeroCard
                {
                    Title = $"ออเดอร์ {request.OrderRequest.OrderCode}",
                    Subtitle = $"ร้าน {request.OrderRequest.Restaurant.Name}{Environment.NewLine}{request.OrderRequest.Restaurant.Address}{Environment.NewLine}ผู้รับ {request.OrderRequest.Customer.Name}{Environment.NewLine}{request.OrderRequest.Customer.Address}{Environment.NewLine}{request.OrderRequest.Customer.Remark}",
                    Text = "รับงานภายใน 30 วินาที",
                    Buttons = new List<CardAction> {
                        new(ActionTypes.ImBack, title: "รับออเดอร์", value: "รับออเดอร์")
                    }
                };

                var attachment = card.ToAttachment();
                var reply = MessageFactory.Attachment(attachment);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Timeup(OrderingRequest request)
        {
            var invalid = request is null || request.ChatBotIds is null || request.ChatBotIds.Count is 0;
            if (invalid) return Ok();

            var conversationReferences = await _referenceRepository.ListConversationReferenceAsync(request.ChatBotIds);
            foreach (var conversationReference in conversationReferences)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, TimeupBotCallback, default(CancellationToken));
            }
            return Ok();

            async Task TimeupBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);

                var messageText = "คุณกดรับไม่ทันเวลาที่กำหนด กรุณารองานถัดไป";
                var reply = MessageFactory.SuggestedActions(_standbyCmd, messageText, null, InputHints.ExpectingInput);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(OrderingRequest request)
        {
            var invalid = request is null || request.ChatBotIds is null || request.ChatBotIds.Count is 0;
            if (invalid) return Ok();

            var conversationReferences = await _referenceRepository.ListConversationReferenceAsync(request.ChatBotIds);
            foreach (var conversationReference in conversationReferences)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, CancelBotCallback, default(CancellationToken));
            }
            return Ok();

            async Task CancelBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);

                userDetails.UnfinishOrder = null;
                await _botStateService.SaveChangesAsync(turnContext);

                var messageText = "ออเดอร์ถูกยกเลิก";
                var reply = MessageFactory.SuggestedActions(_standbyCmd, messageText, null, InputHints.ExpectingInput);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelDeny(OrderingRequest request)
        {
            var invalid = request is null || request.ChatBotIds is null || request.ChatBotIds.Count is 0;
            if (invalid) return Ok();

            var conversationReferences = await _referenceRepository.ListConversationReferenceAsync(request.ChatBotIds);
            foreach (var conversationReference in conversationReferences)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, CancelDenyBotCallback, default(CancellationToken));
            }
            return Ok();

            async Task CancelDenyBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);

                var messageText = "คำขอยกเลิกออเดอร์ถูกปฎิเสธ";
                var reply = MessageFactory.SuggestedActions(_standbyCmd, messageText, null, InputHints.ExpectingInput);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Done(OrderingRequest request)
        {
            var invalid = request is null || request.ChatBotIds is null || request.ChatBotIds.Count is 0;
            if (invalid) return Ok();

            var conversationReferences = await _referenceRepository.ListConversationReferenceAsync(request.ChatBotIds);
            foreach (var conversationReference in conversationReferences)
            {
                await ((BotAdapter)_adapter).ContinueConversationAsync(_appId, conversationReference, DoneBotCallback, default(CancellationToken));
            }
            return Ok();

            async Task DoneBotCallback(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(turnContext, () => new UserDetails(), cancellationToken);

                userDetails.UnfinishOrder = null;
                await _botStateService.SaveChangesAsync(turnContext);

                var messageText = "คุณส่งออเดอร์เรียบร้อย";
                var reply = MessageFactory.SuggestedActions(_standbyCmd, messageText, null, InputHints.ExpectingInput);
                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
        }
    }
}
