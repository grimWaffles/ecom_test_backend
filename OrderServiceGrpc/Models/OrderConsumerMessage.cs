namespace OrderServiceGrpc.Models
{
    public class OrderConsumerMessage
    {
        public int UserId { get; set; }
        public double Amount { get; set; }
        public int OrderId { get; set; }
    }
}
