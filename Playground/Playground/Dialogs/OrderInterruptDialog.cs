using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Playground.Models;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Playground.Dialogs
{
    public class OrderInterruptDialog : ComponentDialog
    {
        public OrderInterruptDialog(string id)
            : base(id)
        {
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
            string messageText = string.Empty;
            Activity promptMessage;
            if (innerDc.Context.Activity.Type == ActivityTypes.Message)
            {
                var text = innerDc.Context.Activity.Text.ToLowerInvariant();

                switch (text)
                {
                    case "ติดต่อ":
                        messageText = $"Admin Solar deilvery{Environment.NewLine}02-12345678";
                        promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                        await innerDc.Context.SendActivityAsync(promptMessage, cancellationToken);
                        return await innerDc.ContinueDialogAsync(cancellationToken);
                    case "ออเดอร์เข้า":
                        var linkAccountDetails = new LinkAccountDetails();
                        return await innerDc.BeginDialogAsync(nameof(LinkAccountDialog), linkAccountDetails, cancellationToken);

                    default: break;
                }
            }

            return null;
        }
    }
}
