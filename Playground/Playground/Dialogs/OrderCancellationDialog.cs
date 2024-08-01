using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Playground.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Playground.Dialogs
{
    public class OrderCancellationDialog : ComponentDialog
    {
        private const string CancelMsgText = "ยกเลิกออเดอร์";
        public OrderCancellationDialog() : base(nameof(OrderCancellationDialog))
        {
            var waterfallSteps = new WaterfallStep[]
                {
                    CancelReasonChoice,
                    Canceling
                };

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> CancelReasonChoice(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var card = new HeroCard
            {
                Title = $"เลือกเหตุผลของการยกเลิกออเดอร์",
                Buttons = new List<CardAction> {
                    new(ActionTypes.ImBack, title: "ติดต่อลูกค้าไม่ได้เกิน 3 ครั้ง", value: "ยกเลิก เพราะติดต่อลูกค้าไม่ได้เกิน 3 ครั้ง"),
                    new(ActionTypes.ImBack, title: "ร้านอาหารปิด", value: "ยกเลิก เพราะร้านอาหารปิด"),
                    new(ActionTypes.ImBack, title: "เกิดอุบัติเหตุระหว่างจัดส่ง", value: "ยกเลิก เพราะเกิดอุบัติเหตุระหว่างจัดส่ง"),
                    new(ActionTypes.ImBack, title: "ร้านอาหารไม่สามารถให้บริการได้", value: "ยกเลิก เพราะร้านอาหารไม่สามารถให้บริการได้"),
                    new(ActionTypes.ImBack, title: "ลูกค้าแจ้งพิกัดผิด", value: "ยกเลิก เพราะลูกค้าแจ้งพิกัดผิด"),
                    new(ActionTypes.ImBack, title: "เกิดปัญหาจากระบบ", value: "ยกเลิก เพราะเกิดปัญหาจากระบบ"),
                    new(ActionTypes.ImBack, title: "ร้านอาหารทำอาหาร", value: "ยกเลิก เพราะร้านอาหารทำอาหาร"),
                    new(ActionTypes.ImBack, title: "อื่นๆ", value: "ยกเลิก เพราะอื่นๆ"),
                }
            };
            var promptOptions = new PromptOptions
            {
                Prompt = (Activity)MessageFactory.Attachment(card.ToAttachment()),
                Style = ListStyle.HeroCard,
            };
            return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
        }

        private async Task<DialogTurnResult> Canceling(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var answer = stepContext.Result as string;
            switch (answer)
            {
                case "ยกเลิก เพราะติดต่อลูกค้าไม่ได้เกิน 3 ครั้ง":
                case "ยกเลิก เพราะร้านอาหารปิด":
                case "ยกเลิก เพราะเกิดอุบัติเหตุระหว่างจัดส่ง":
                case "ยกเลิก เพราะร้านอาหารไม่สามารถให้บริการได้":
                case "ยกเลิก เพราะลูกค้าแจ้งพิกัดผิด":
                case "ยกเลิก เพราะเกิดปัญหาจากระบบ":
                case "ยกเลิก เพราะร้านอาหารทำอาหาร":
                case "ยกเลิก เพราะอื่นๆ":
                    var details = new OrderDetails { Status = OrderStatus.Cancel, Remark = answer };
                    return await stepContext.EndDialogAsync(details, cancellationToken);

                default:
                    return await stepContext.CancelAllDialogsAsync(cancellationToken);
            }
        }
    }
}
