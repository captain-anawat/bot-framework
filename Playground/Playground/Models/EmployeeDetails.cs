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
    public class OrderResponse
    {
        public string _id { get; set; }
        public string OrderCode { get; set; }
        public string ManaEndpoint { get; set; }
        public ContactInfo Restaurant { get; set; }
        public ContactInfo Customer { get; set; }
    }
    public class ContactInfo
    {
        public string _id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string Remark { get; set; }
    }
}
