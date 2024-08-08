namespace Playground.Models
{
    public class EmployeeDetails
    {
        public string _id { get; set; }
        public string DeliveryName { get; set; }
        public string Address { get; set; }
        public bool OnWorkStatus { get; set; }
        public bool Suspended { get; set; }
        public string PhoneNumber { get; set; }
        public OrderResponse OrderRequest { get; set; }
    }
}
