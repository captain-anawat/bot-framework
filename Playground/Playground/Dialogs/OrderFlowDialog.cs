using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Playground.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Playground.Dialogs
{
    public class OrderFlowDialog : OrderInterruptDialog
    {
        public OrderFlowDialog(OrderCancellationDialog orderInterruptDialog) : base(nameof(OrderFlowDialog))
        {
            AddDialog(orderInterruptDialog);

            var waterfallSteps = new WaterfallStep[]
            {
                ComingIn,
                OnProcess,
                UpdateStatus,
                Canceling
            };

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> ComingIn(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (OrderDetails)stepContext.Options;
            if (details.OrderAccept) return await stepContext.NextAsync("รับออเดอร์", cancellationToken);
            var card = new HeroCard
            {
                Title = "ออเดอร์ 12345",
                Subtitle = $"ร้าน xx{Environment.NewLine}xxxxx{Environment.NewLine}ผู้รับ xx{Environment.NewLine}xxxx",
                Text = "รับงานภายใน 30 วินาที",
                Buttons = new List<CardAction> {
                    new(ActionTypes.ImBack, title: "รับออเดอร์", value: "รับออเดอร์")
                }
            };

            var promptOptions = new PromptOptions
            {
                Prompt = (Activity)MessageFactory.Attachment(card.ToAttachment()),
                Style = ListStyle.HeroCard,
            };
            return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
        }
        private async Task<DialogTurnResult> OnProcess(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (OrderDetails)stepContext.Options;
            var answer = stepContext.Result as string;
            HeroCard card = null;
            PromptOptions promptOptions = null;
            switch (answer)
            {
                case "รับออเดอร์":
                    details.OrderAccept = true;
                    card = new HeroCard
                    {
                        Title = "ดูข้อมูลออเดอร์หรืออัพเดทสถานะออเดอร์",
                        Subtitle = "ดูข้อมูลออเดอร์หรืออัพเดทสถานะออเดอร์ ผ่านจากลิงค์นี้ https://www.google.com/",
                        Buttons = new List<CardAction> {
                            new(ActionTypes.OpenUrl, title: "ดูออเดอร์", value: "https://www.google.com/"),
                            new(ActionTypes.ImBack, title: "จบงาน", value: "จบงาน"),
                        }
                    };

                    promptOptions = new PromptOptions
                    {
                        Prompt = (Activity)MessageFactory.Attachment(card.ToAttachment()),
                        Style = ListStyle.HeroCard,
                    };
                    return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);

                default:
                    return await stepContext.ReplaceDialogAsync(nameof(OrderFlowDialog), stepContext.Options, cancellationToken);
            }
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
        private async Task<DialogTurnResult> Canceling(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (OrderDetails)stepContext.Options;
            switch (stepContext.Result)
            {
                case OrderDetails result:
                    details.Status = result.Status;
                    details.Remark = result.Remark;
                    string messageText = $"สถานะการปิดงาน {result.Status.ToString()}{Environment.NewLine}{result.Remark}";
                    var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                    await stepContext.Context.SendActivityAsync(promptMessage, cancellationToken);
                    return await stepContext.EndDialogAsync(details, cancellationToken);

                default:
                    return await stepContext.ReplaceDialogAsync(nameof(OrderFlowDialog), stepContext.Options, cancellationToken);
            }
        }
    }
}
