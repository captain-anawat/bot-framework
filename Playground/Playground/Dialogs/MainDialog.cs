using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using System;
using Playground.Models;

namespace Playground.Dialogs
{
    public enum switchTo
    {
        Ready,
        NotReady
    }
    public class MainDialog : ComponentDialog
    {
        private readonly ILogger _logger;
        private bool _isLinkedAccount;
        private bool _isReady;
        private readonly string _readyCmd = "เปิด";
        private readonly string _notReadyCmd = "ปิด";
        private readonly string _contractCmd = "ติดต่อ";
        private bool _switchReadying = false;

        public MainDialog(LinkAccountDialog linkAccountDialog, OrderFlowDialog orderFlowDialog, ILogger<MainDialog> logger)
            : base(nameof(MainDialog))
        {
            _logger = logger;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(linkAccountDialog);
            AddDialog(orderFlowDialog);

            var waterfallSteps = new WaterfallStep[]
            {
                Rider_IntroStepAsync,
                Rider_ActStepAsync,
                Rider_FinalStepAsync,
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
                var text = innerDc.Context.Activity.Text.ToLowerInvariant();

                switch (text)
                {
                    case "รับออเดอร์":
                        var orderDetails = new OrderDetails { OrderAccept = true };
                        return await innerDc.BeginDialogAsync(nameof(OrderFlowDialog), orderDetails, cancellationToken);

                    default: break;
                }
            }

            return null;
        }
        private async Task<DialogTurnResult> Rider_IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (_isLinkedAccount)
            {
                var messageText = $"สถานะไรเดอร์ {riderStatus()}";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = promptMessage,
                    Choices = new[]
                    {
                        new Choice { Value = _readyCmd },
                        new Choice { Value = _notReadyCmd },
                        new Choice { Value = _contractCmd }
                    }
                }, cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }

            string riderStatus()
            {
                return _isReady ? "เปิด" : "ปิด";
            }
        }
        private async Task<DialogTurnResult> Rider_ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (_isLinkedAccount)
            {
                var choic = (FoundChoice)stepContext.Result;
                string messageText = string.Empty;
                Activity promptMessage;
                switch (choic.Value)
                {
                    case "เปิด":
                        stepContext.Values["SwitchTo"] = switchTo.Ready;
                        messageText = "คุณต้องการเปิดรับงานใช่หรือไม่";
                        promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                        return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);

                    case "ปิด":
                        stepContext.Values["SwitchTo"] = switchTo.NotReady;
                        messageText = "คุณต้องการปิดรับงานใช่หรือไม่";
                        promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                        return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);

                    case "ติดต่อ": messageText = $"Admin Solar deilvery{Environment.NewLine}02-12345678"; break;
                    default:
                        messageText = "ระบบไม่เข้าใจคำขอของคุณ";
                        promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                        await stepContext.Context.SendActivityAsync(promptMessage, cancellationToken);
                        break;
                }
            }
            else
            {
                var linkAccountDetails = new LinkAccountDetails();
                return await stepContext.BeginDialogAsync(nameof(LinkAccountDialog), linkAccountDetails, cancellationToken);
            }

            return await stepContext.NextAsync(null, cancellationToken);
        }
        private async Task<DialogTurnResult> Rider_FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            switch (stepContext.Result)
            {
                case LinkAccountDetails laResult:
                    _isLinkedAccount = true;
                    break;

                case bool srResult when srResult:
                    _isReady = (switchTo)stepContext.Values["SwitchTo"] switch
                    {
                        switchTo.Ready => true,
                        switchTo.NotReady => false,
                        _ => _isReady
                    };
                    stepContext.Values.Remove("SwitchTo");
                    break;

                default:
                    break;
            }

            // Restart the main dialog with a different message the second time around
            var promptMessage2 = "What else can I do for you?";
            return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage2, cancellationToken);
        }
    }
}
