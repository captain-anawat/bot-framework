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
    public class LinkAccountDialog : ComponentDialog
    {
        public LinkAccountDialog()
            : base(nameof(LinkAccountDialog))
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            var waterfallSteps = new WaterfallStep[] {
                LinkingStepAsync,
                FinalStepAsync
            };

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> LinkingStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var reply = MessageFactory.Attachment(new Attachment
            {
                ContentType = "image/png",
                ContentUrl = "https://5.imimg.com/data5/SELLER/Default/2022/10/RR/YR/YM/13168808/cu-qr-codes-chennai-website-developers--500x500.png",
            });

            await stepContext.Context.SendActivityAsync(reply, cancellationToken);

            var card = new HeroCard
            {
                Title = "กรุณาแสกน qr ผูกบัญชีกับมานะ เพื่อเข้าใช้งานระบบ",
                Buttons = new List<CardAction> {
                    new(ActionTypes.OpenUrl, title: "เปิดแอพ มานะ", value: "https://www.google.com/"),
                    new(ActionTypes.ImBack, title: "แสกน qr", value: "แสกน qr")
                }
            };

            var promptOptions = new PromptOptions
            {
                Prompt = (Activity)MessageFactory.Attachment(card.ToAttachment()),
                Style = ListStyle.HeroCard,
            };
            return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
        }
        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (LinkAccountDetails)stepContext.Options;

            var choice = stepContext.Result;
            details.Scanned = choice.ToString();

            var messageText = $"คุณได้ทำการผูก line account กับ mana เรียบร้อยแล้ว{Environment.NewLine}ยินดีต้อนรับร้าน wib cafe";
            var reply = MessageFactory.Text(messageText, messageText);
            await stepContext.Context.SendActivityAsync(reply, cancellationToken);
            return await stepContext.EndDialogAsync(details, cancellationToken);
        }
    }
}
