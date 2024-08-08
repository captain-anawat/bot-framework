using Playground.Dialogs;

namespace Playground.Models
{
    public class UserDetails
    {
        public string RiderId { get; set; }
        public bool IsLinkedAccount { get; set; }
        public string UnfinishOrder { get; set; }
        public string RequestOrder { get; set; }
        public SwitchTo SwitchState { get; set; }
    }
}
