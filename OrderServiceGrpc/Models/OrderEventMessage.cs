namespace OrderServiceGrpc.Models
{
    public class OrderEventMessage
    {
        public int OrderId { get; set; }
        public double Amount { get; set; }
        public int UserId { get; set; }
    }
}
