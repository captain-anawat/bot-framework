namespace Playground.Models
{
    public class OrderDetails
    {
        public string OrderId { get; set; }
        public bool OrderAccept { get; set; }
        public OrderStatus Status { get; set; }
        public string Remark { get; set; }
    }

    public enum OrderStatus
    {
        Cancel,
        Done
    }
}
