﻿using Microsoft.Bot.Builder;
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
                UpdateStatus,
                Canceling
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
                Subtitle = "ดูข้อมูลออเดอร์หรืออัพเดทสถานะออเดอร์ ผ่านจากลิงค์นี้ https://www.google.com/",
                Buttons = new List<CardAction> {
                            new(ActionTypes.OpenUrl, title: "ดูออเดอร์", value: "https://www.google.com/"),
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
