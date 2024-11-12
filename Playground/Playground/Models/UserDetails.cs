namespace Playground.Models
{
    public class UserDetails
    {
        public bool IsLinkedAccount { get; set; }
        public string RiderId { get; set; }
        public string UserName { get; set; }
        public string DeliveryName { get; set; }
        public string PhoneNumber { get; set; }
        public bool? WorkStatus { get; set; }
        public string UnfinishOrder { get; set; }
        public ConfirmCase ConfirmAction { get; set; }
    }
}
