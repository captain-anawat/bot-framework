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
        private readonly IList<Choice> _riderCmd =
            [
                new Choice { Value = "เปิด" },
                new Choice { Value = "ปิด" },
                new Choice { Value = "ติดต่อ" }
            ];
        private readonly IList<Choice> _confirmCmd =
            [
                new Choice { Value = "ยืนยัน" },
                new Choice { Value = "ยกเลิก" }
            ];
        private readonly string _replaceDialogMessage = "restart dialog";
        private readonly IBotStateService _botStateService;
        private readonly IRestClientService _restClientService;
        private readonly IUserDetailService _userDetailService;
        private readonly ILogger _logger;
        private readonly ConnectionSettings _connectionSettings;

        public MainDialog(IBotStateService botStateService, IRestClientService restClientService, IUserDetailService userDetailService,ILogger<MainDialog> logger, ConnectionSettings connectionSettings)
            : base(nameof(MainDialog))
        {
            _botStateService = botStateService;
            _restClientService = restClientService;
            _userDetailService = userDetailService;
            _logger = logger;
            _connectionSettings = connectionSettings;
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));

            var waterfallSteps = new WaterfallStep[]
            {
                PrepareStepAsync,
                StandardActStepAsync,
                ConfirmActStepAsync,
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
                UserDetails userDetails = await _botStateService.UserDetailsAccessor.GetAsync(innerDc.Context, () => new UserDetails(), cancellationToken);
                string messageText = null;
                IMessageActivity messageActivity = null;
                bool isRestartDialog = true;
                var text = innerDc.Context.Activity.Text.ToLowerInvariant();

                switch (text)
                {
                    case "งานย้อนหลัง" when userDetails.IsLinkedAccount:
                        messageActivity = CreateHeroCardWithUrl("ประวัติงานย้อนหลัง", _connectionSettings.HistoryPageUrl);
                        break;

                    case "โปรไฟล์" when userDetails.IsLinkedAccount:
                        messageActivity = CreateHeroCardWithUrl("โปรไฟล์", _connectionSettings.ProfilePageUrl);
                        break;

                    case "รับออเดอร์" when userDetails.IsLinkedAccount:
                        if (string.IsNullOrWhiteSpace(userDetails.RequestOrder))
                        {
                            messageText = "หมดเวลารับออเดอร์ กรุณารอออเดอร์ถัดไป";
                            messageActivity = MessageFactory.Text(messageText, messageText);
                            break;
                        }
                        var acceptOrderApi = $"{_connectionSettings.DeliveryAPIBaseUrl}/api/Rider/RiderAcceptOrder/{userDetails.RiderId}/{userDetails.RequestOrder}";
                        await _restClientService.Put(acceptOrderApi, innerDc.Context.Activity.From.Id, string.Empty);
                        userDetails.UnfinishOrder = userDetails.RequestOrder;
                        userDetails.RequestOrder = string.Empty;
                        await _botStateService.SaveChangesAsync(innerDc.Context);
                        break;

                    case "reset":
                        userDetails.IsLinkedAccount = false;
                        userDetails.RiderId = null;
                        await _botStateService.SaveChangesAsync(innerDc.Context);
                        var userId = innerDc.Context.Activity.From.Id;
                        var resetApi = $"{_connectionSettings.DeliveryAPIBaseUrl}/api/AdminWeb/LinkedRemove/{userId}";
                        await _restClientService.Put(resetApi, innerDc.Context.Activity.From.Id, string.Empty);
                        break;

                    case "ตกลง":
                    case "พร้อมเริ่มงาน":
                    case "เริ่มผูกบัญชีใหม่":
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
                else if (!userDetails.IsLinkedAccount)
                {
                    messageText = "คุณยังไม่ได้ผูก line account กับ mana";
                    var promptMessage = MessageFactory.Text(messageText, messageText);
                    await innerDc.Context.SendActivityAsync(promptMessage, cancellationToken);
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
                userDetails = await _userDetailService.TryGetUserDetail(userDetails, stepContext.Context.Activity.From.Id);
                await _botStateService.SaveChangesAsync(stepContext.Context);
                if (!userDetails.IsLinkedAccount)
                {
                    var userId = stepContext.Context.Activity.From.Id;
                    var userName = stepContext.Context.Activity.From.Name;
                    var sessionRequest = $"{_connectionSettings.DeliveryAPIBaseUrl}/api/AdminWeb/LinkRequest/line/{userId}/{userName}";
                    var session = await _restClientService.Get<Session>(sessionRequest, userId);

                    var reply = MessageFactory.Attachment(new Attachment
                    {
                        ContentType = "image/png",
                        ContentUrl = session.Url,
                    });
                    await stepContext.Context.SendActivityAsync(reply, cancellationToken);

                    var redirectToDeeplinkUrl = $"{_connectionSettings.RedirectManaDeeplinkBaseUrl}?np_url={session.Url}";
                    var promptOptions = new PromptOptions
                    {
                        Prompt = CreateHeroCardWithUrl("กรุณาแสกน qr ผูกบัญชีกับมานะ เพื่อเข้าใช้งานระบบ", redirectToDeeplinkUrl, "เปิดแอพ มานะ"),
                        Choices = _riderCmd,
                    };
                    return await stepContext.PromptAsync(nameof(ChoicePrompt), promptOptions, cancellationToken);
                }
            }

            if (userDetails.RiderId.StartsWith("mrid"))
            {
                var messageText = $"คุณยังไม่ได้เป็นไรเดอร์ของเดริเวอรี่";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = promptMessage,
                    Choices = _riderCmd
                }, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(userDetails.UnfinishOrder))
            {
                var reply = CreateHeroCardWithUrl("ดูข้อมูลออเดอร์หรืออัพเดทสถานะออเดอร์", _connectionSettings.OrderStagePageUrl);
                await stepContext.Context.SendActivityAsync(reply, cancellationToken);

                var messageText = $"สถานะไรเดอร์ กำลังวิ่งงาน";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = promptMessage,
                    Choices = _riderCmd
                }, cancellationToken);
            }
            else
            {
                var riderStatus = userDetails.WorkStatus.Value ? "เปิด" : "ปิด";
                var messageText = $"สถานะไรเดอร์ {riderStatus}";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = promptMessage,
                    Choices = _riderCmd
                }, cancellationToken);
            }
        }
        private async Task<DialogTurnResult> StandardActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(stepContext.Context, () => new UserDetails(), cancellationToken);
            string messageText;
            Activity promptMessage;

            if (userDetails.RiderId.StartsWith("mrid"))
            {
                messageText = "คุณยังไม่ได้เข้าร่วมกับ delivery นี้";
                promptMessage = MessageFactory.Text(messageText, messageText);
                await stepContext.Context.SendActivityAsync(promptMessage, cancellationToken);
                return await stepContext.NextAsync(null, cancellationToken);
            }

            var choice = stepContext.Result as FoundChoice;
            if (choice is null)
            {
                // Restart the main dialog with a different message the second time around
                return await stepContext.ReplaceDialogAsync(InitialDialogId, _replaceDialogMessage, cancellationToken);
            }

            switch (choice.Value)
            {
                case "เปิด" when !string.IsNullOrWhiteSpace(userDetails.UnfinishOrder):
                case "ปิด" when !string.IsNullOrWhiteSpace(userDetails.UnfinishOrder):
                    messageText = $"คุณไม่สามารถใช้คำสั่ง {choice.Value} ได้ระหว่างกำลังวิ่งงาน";
                    promptMessage = MessageFactory.Text(messageText, messageText);
                    await stepContext.Context.SendActivityAsync(promptMessage, cancellationToken);
                    break;

                case "เปิด" when userDetails.WorkStatus.Value:
                case "ปิด" when !userDetails.WorkStatus.Value:
                    messageText = $"สถานะของคุณเป็น {choice.Value} รับงานอยู่แล้ว";
                    promptMessage = MessageFactory.Text(messageText, messageText);
                    await stepContext.Context.SendActivityAsync(promptMessage, cancellationToken);
                    break;

                case "เปิด":
                case "ปิด":
                    userDetails.ConfirmAction = choice.Value is "เปิด" ? ConfirmCase.SwitchToOnWork : ConfirmCase.SwitchToOffWork;
                    await _botStateService.SaveChangesAsync(stepContext.Context);
                    messageText = $"คุณต้องการ {choice.Value} รับงานใช่หรือไม่";
                    promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                    return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                    {
                        Prompt = promptMessage,
                        Choices = _confirmCmd
                    }, cancellationToken);

                case "ติดต่อ":
                    messageText = $"Admin {userDetails.DeliveryName} deilvery{Environment.NewLine}{userDetails.PhoneNumber}";
                    promptMessage = MessageFactory.Text(messageText, messageText);
                    await stepContext.Context.SendActivityAsync(promptMessage, cancellationToken);
                    break;
            }

            // Restart the main dialog with a different message the second time around
            return await stepContext.ReplaceDialogAsync(InitialDialogId, _replaceDialogMessage, cancellationToken);
        }
        private async Task<DialogTurnResult> ConfirmActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userDetails = await _botStateService.UserDetailsAccessor.GetAsync(stepContext.Context, () => new UserDetails(), cancellationToken);
            var choice = stepContext.Result as FoundChoice;
            if (choice is null)
            {
                // Restart the main dialog with a different message the second time around
                return await stepContext.ReplaceDialogAsync(InitialDialogId, _replaceDialogMessage, cancellationToken);
            }

            if (choice.Value is "ยืนยัน")
            {
                switch (userDetails.ConfirmAction)
                {
                    case ConfirmCase.SwitchToOnWork:
                        var turnOnApi = $"{_connectionSettings.DeliveryAPIBaseUrl}/api/Rider/RiderWorkStatusTurnOn/{userDetails.RiderId}";
                        await updateRiderStatus(turnOnApi);
                        break;

                    case ConfirmCase.SwitchToOffWork:
                        var turnOffApi = $"{_connectionSettings.DeliveryAPIBaseUrl}/api/Rider/RiderWorkStatusTurnOff/{userDetails.RiderId}";
                        await updateRiderStatus(turnOffApi);
                        break;
                }
            }

            // Restart the main dialog with a different message the second time around
            return await stepContext.ReplaceDialogAsync(InitialDialogId, _replaceDialogMessage, cancellationToken);

            async Task updateRiderStatus(string apiStr)
            {
                var response = await _restClientService.Put<EmployeeDetails>(apiStr, stepContext.Context.Activity.From.Id, string.Empty);
                userDetails.WorkStatus = response is not null ? response.OnWorkStatus : userDetails.WorkStatus;
                await _botStateService.SaveChangesAsync(stepContext.Context);
            }
        }

        private Activity CreateHeroCardWithUrl(string title, string url, string buttonText = null)
        {
            var card = new HeroCard
            {
                Title = title,
                Buttons = new List<CardAction>
                {
                    new(ActionTypes.OpenUrl, title: buttonText ?? "เปิด", value: url),
                }
            };
            return (Activity)MessageFactory.Attachment(card.ToAttachment());
        }
    }
}
