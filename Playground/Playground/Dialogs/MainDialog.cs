using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Playground.Models;
using Playground.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Playground.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly string APIBaseUrl = "https://delivery-3rd-test-api.azurewebsites.net";
        private readonly string HistoryPageUrl = "https://devster-delivery-test.onmana.space/apprider/index.html#/history-main";
        private readonly string ProfilePageUrl = "https://devster-delivery-test.onmana.space/apprider/index.html#/profile-main";
        private readonly string OrderStagePageUrl = "https://devster-delivery-test.onmana.space/apprider/index.html#/order-stage";
        private EmployeeDetails _employeeDetails;
        private readonly IBotStateService _botStateService;
        private readonly IRestClientService _restClientService;
        private readonly ILogger _logger;
        private readonly string _replaceDialogMessage = "restart dialog";

        public MainDialog(IBotStateService botStateService, IRestClientService restClientService, ILogger<MainDialog> logger)
            : base(nameof(MainDialog))
        {
            _botStateService = botStateService;
            _restClientService = restClientService;
            _logger = logger;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            var waterfallSteps = new WaterfallStep[]
            {
                PrepareStepAsync,
                StandardActStepAsync,
                FinalStepAsync,
            };

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }
        protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default)
        {
            var result = await InterruptAsync(innerDc, cancellationToken);
            if (result != null)
            {
                return result;
            }

            return await base.OnContinueDialogAsync(innerDc, cancellationToken);
        }
        private async Task<DialogTurnResult> InterruptAsync(DialogContext innerDc, CancellationToken cancellationToken)
        {
            if (innerDc.Context.Activity.Type == ActivityTypes.Message)
            {
                UserDetails userDetails = null;
                IMessageActivity messageActivity = null;
                bool isRestartDialog = true;
                var text = innerDc.Context.Activity.Text.ToLowerInvariant();
                switch (text)
                {
                    case "งานย้อนหลัง":
                        messageActivity = CreateHeroCardWithUrl("ประวัติงานย้อนหลัง", HistoryPageUrl);
                        break;
                    case "โปรไฟล์":
                        messageActivity = CreateHeroCardWithUrl("โปรไฟล์", ProfilePageUrl);
                        break;
                    case "รับออเดอร์":
                        userDetails = await _botStateService.UserDetailsAccessor.GetAsync(innerDc.Context, () => new UserDetails(), cancellationToken);
                        if (string.IsNullOrWhiteSpace(userDetails.RequestOrder))
                        {
                            var messageText = "หมดเวลารับออเดอร์ กรุณารอออเดอร์ถัดไป";
                            messageActivity = MessageFactory.Text(messageText, messageText);
                            break;
                        }
                        var acceptOrderApi = $"{APIBaseUrl}/api/Rider/RiderAcceptOrder/{userDetails.RiderId}/{userDetails.RequestOrder}";
                        await _restClientService.Put(acceptOrderApi, string.Empty);
                        userDetails.UnfinishOrder = userDetails.RequestOrder;
                        userDetails.RequestOrder = string.Empty;
                        await _botStateService.SaveChangesAsync(innerDc.Context);
                        break;
                    case "reset":
                        userDetails = await _botStateService.UserDetailsAccessor.GetAsync(innerDc.Context, () => new UserDetails(), cancellationToken);
                        userDetails.IsLinkedAccount = false;
                        userDetails.RiderId = null;
                        await _botStateService.SaveChangesAsync(innerDc.Context);
                        break;
                    default:
                        isRestartDialog = false;
                        break;
                }

                if (messageActivity is not null)
                {
                    await innerDc.Context.SendActivityAsync(messageActivity, cancellationToken);
                }
                if (isRestartDialog)
                {
                    // Restart the main dialog with a different message the second time around
                    return await innerDc.ReplaceDialogAsync(InitialDialogId, _replaceDialogMessage, cancellationToken);
                }
            }
            return null;
        }
        private async Task<DialogTurnResult> PrepareStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(stepContext.Context, () => new UserDetails(), cancellationToken);
            if (!userDetails.IsLinkedAccount)
            {
                await TryGetUserDetail();
                if (!userDetails.IsLinkedAccount)
                {
                    var userId = stepContext.Context.Activity.From.Id;
                    var userName = stepContext.Context.Activity.From.Name;
                    var sessionRequest = $"{APIBaseUrl}/api/AdminWeb/LinkRequest/line/{userId}/{userName}";
                    var session = await _restClientService.Get<Session>(sessionRequest);

                    var reply = MessageFactory.Attachment(new Attachment
                    {
                        ContentType = "image/png",
                        ContentUrl = session.Url,
                    });

                    await stepContext.Context.SendActivityAsync(reply, cancellationToken);

                    var card = new HeroCard
                    {
                        Title = "กรุณาแสกน qr ผูกบัญชีกับมานะ เพื่อเข้าใช้งานระบบ",
                        Buttons = new List<CardAction> {
                            new(ActionTypes.OpenUrl, title: "เปิดแอพ มานะ", value: "https://www.google.com/")
                        }
                    };

                    var promptOptions = new PromptOptions
                    {
                        Prompt = (Activity)MessageFactory.Attachment(card.ToAttachment()),
                        Style = ListStyle.HeroCard,
                    };
                    return await stepContext.PromptAsync(nameof(ChoicePrompt), promptOptions, cancellationToken);
                }
                return await stepContext.NextAsync(null, cancellationToken);
            }
            if (!string.IsNullOrWhiteSpace(userDetails.UnfinishOrder))
            {
                var reply = CreateHeroCardWithUrl("ข้อมูลออเดอร์หรืออัพเดทสถานะออเดอร์", OrderStagePageUrl);
                await stepContext.Context.SendActivityAsync(reply, cancellationToken);

                var messageText = $"สถานะไรเดอร์ กำลังวิ่งงาน";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = promptMessage,
                    Choices = new[]
                    {
                        new Choice { Value = "ติดต่อ" }
                    }
                }, cancellationToken);
            }
            if (userDetails.RiderId.StartsWith("mrid"))
            {
                var messageText = $"คุณยังไม่ได้เป็นไรเดอร์ของเดริเวอรี่";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = promptMessage,
                    Choices = new[]
                    {
                        new Choice { Value = "ติดต่อ" }
                    }
                }, cancellationToken);
            }
            else
            {
                if (_employeeDetails is null)
                {
                    var riderDetailsApi = $"{APIBaseUrl}/api/Rider/GetRiderInfo/{userDetails.RiderId}";
                    _employeeDetails = await _restClientService.Get<EmployeeDetails>(riderDetailsApi);
                }
                var isReady = _employeeDetails.OnWorkStatus;
                var messageText = $"สถานะไรเดอร์ {riderStatus(isReady)}";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = promptMessage,
                    Choices = new[]
                    {
                        new Choice { Value = swtichTo(isReady) },
                        new Choice { Value = "ติดต่อ" }
                    }
                }, cancellationToken);
            }

            async Task TryGetUserDetail()
            {
                var userId = stepContext.Context.Activity.From.Id;
                var apiStr = $"{APIBaseUrl}/api/Rider/GetRiderInfoWithChatBotId/{userId}";
                var info = await _restClientService.Get<EmployeeDetails>(apiStr);
                if (info is null) return;
                var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(stepContext.Context, () => new UserDetails(), cancellationToken);
                userDetails.IsLinkedAccount = true;
                userDetails.RiderId = info._id;
                apiStr = $"{APIBaseUrl}/api/Rider/GetUnfinishedOrder/{info._id}";
                var order = await _restClientService.Get<OrderResponse>(apiStr);
                if (order is not null)
                {
                    userDetails.UnfinishOrder = order._id;
                }
                await _botStateService.SaveChangesAsync(stepContext.Context);
            }
            string swtichTo(bool isReady)
            {
                return isReady ? "ปิด" : "เปิด";
            }
            string riderStatus(bool isReady)
            {
                return isReady ? "เปิด" : "ปิด";
            }
        }
        private async Task<DialogTurnResult> StandardActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(stepContext.Context, () => new UserDetails(), cancellationToken);
            var message = (FoundChoice)stepContext.Result;
            string messageText = string.Empty;
            Activity promptMessage;
            switch (message.Value)
            {
                case "เปิด":
                    userDetails.SwitchState = SwitchTo.Ready;
                    await _botStateService.SaveChangesAsync(stepContext.Context);
                    messageText = "คุณต้องการเปิดรับงานใช่หรือไม่";
                    promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                    return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);

                case "ปิด":
                    userDetails.SwitchState = SwitchTo.NotReady;
                    await _botStateService.SaveChangesAsync(stepContext.Context);
                    messageText = "คุณต้องการปิดรับงานใช่หรือไม่";
                    promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                    return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);

                case "ติดต่อ":
                    messageText = $"Admin {_employeeDetails.DeliveryName} deilvery{Environment.NewLine}{_employeeDetails.PhoneNumber}";
                    promptMessage = MessageFactory.Text(messageText, messageText);
                    await stepContext.Context.SendActivityAsync(promptMessage, cancellationToken);
                    break;

                default:
                    messageText = "ระบบไม่เข้าใจคำขอของคุณ";
                    promptMessage = MessageFactory.Text(messageText, messageText);
                    await stepContext.Context.SendActivityAsync(promptMessage, cancellationToken);
                    break;
            }
            return await stepContext.NextAsync(null, cancellationToken);
        }
        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(stepContext.Context, () => new UserDetails(), cancellationToken);
            switch (stepContext.Result)
            {
                case bool srResult when srResult:
                    EmployeeDetails response;
                    switch (userDetails.SwitchState)
                    {
                        case SwitchTo.Ready:
                            var turnOnApi = $"{APIBaseUrl}/api/Rider/RiderWorkStatusTurnOn/{userDetails.RiderId}";
                            response = await _restClientService.Put<EmployeeDetails>(turnOnApi, string.Empty);
                            _employeeDetails = response is not null ? response : _employeeDetails;
                            break;

                        case SwitchTo.NotReady:
                            var turnOffApi = $"{APIBaseUrl}/api/Rider/RiderWorkStatusTurnOff/{userDetails.RiderId}";
                            response = await _restClientService.Put<EmployeeDetails>(turnOffApi, string.Empty);
                            _employeeDetails = response is not null ? response : _employeeDetails;
                            break;

                        default:
                            _employeeDetails = null;
                            break;
                    }
                    break;

                default:
                    break;
            }

            // Restart the main dialog with a different message the second time around
            return await stepContext.ReplaceDialogAsync(InitialDialogId, _replaceDialogMessage, cancellationToken);
        }

        private Activity CreateHeroCardWithUrl(string title, string url)
        {
            var card = new HeroCard
            {
                Title = title,
                Subtitle = $"ดูผ่านจากลิงค์นี้ {url}",
                Buttons = new List<CardAction>
                {
                    new(ActionTypes.OpenUrl, title: "เปิดลิงคิ์", value: url),
                }
            };
            return (Activity)MessageFactory.Attachment(card.ToAttachment());
        }
    }
}
