using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Playground.Models;
using Playground.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Playground.Dialogs
{
    public class OrderFlowDialog : OrderInterruptDialog
    {
        private readonly IRestClientService _restClientService;

        public OrderFlowDialog(IRestClientService restClientService, OrderCancellationDialog orderInterruptDialog) : base(nameof(OrderFlowDialog))
        {
            AddDialog(orderInterruptDialog);

            var waterfallSteps = new WaterfallStep[]
            {
                OnProcess,
                UpdateStatus
            };

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));

            InitialDialogId = nameof(WaterfallDialog);
            _restClientService = restClientService;
        }

        private async Task<DialogTurnResult> OnProcess(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (OrderDetails)stepContext.Options;
            var card = new HeroCard
            {
                Title = "ดูข้อมูลออเดอร์หรืออัพเดทสถานะออเดอร์",
                Subtitle = "ผ่านจากลิงค์นี้ https://devster-delivery-test.onmana.space/apprider/index.html#/order-stage",
                Buttons = new List<CardAction> {
                            new(ActionTypes.OpenUrl, title: "เปิดลิงคิ์", value: "https://devster-delivery-test.onmana.space/apprider/index.html#/order-stage"),
                            new(ActionTypes.ImBack, title: "จบงาน", value: "จบงาน"),
                        }
            };

            var promptOptions = new PromptOptions
            {
                Prompt = (Activity)MessageFactory.Attachment(card.ToAttachment()),
                Style = ListStyle.HeroCard,
            };
            return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
        }
        private async Task<DialogTurnResult> UpdateStatus(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (OrderDetails)stepContext.Options;
            switch (stepContext.Result)
            {
                case "จบงาน":
                    details.Status = OrderStatus.Done;
                    string messageText = $"สถานะการปิดงาน {details.Status.ToString()}";
                    var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                    await stepContext.Context.SendActivityAsync(promptMessage, cancellationToken);
                    return await stepContext.EndDialogAsync(details, cancellationToken);

                case "cancel":
                case "quit":
                case "ยกเลิก":
                    var orderDetails = (OrderDetails)stepContext.Options;
                    return await stepContext.BeginDialogAsync(nameof(OrderCancellationDialog), orderDetails, cancellationToken);

                default:
                    return await stepContext.ReplaceDialogAsync(nameof(OrderFlowDialog), stepContext.Options, cancellationToken);
            }
        }
    }
}
