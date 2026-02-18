namespace OrderServiceGrpc.Models.Dtos
{
    public class CreateOrderRequestDto
    {
        public OrderDto Order { get; set; }
        public int UserId { get; set; }
    }

    public class OrderDto
    {
        public int Id { get; set; }
        public TimestampDto OrderDate { get; set; }
        public int OrderCounter { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; }
        public double NetAmount { get; set; }
        public int CreatedBy { get; set; }
        public TimestampDto CreatedDate { get; set; }
        public TimestampDto ModifiedDate { get; set; }
        public int ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public List<OrderItemDto> Items { get; set; }
    }

    public class OrderItemDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public double GrossAmount { get; set; }
        public string Status { get; set; }
        public int CreatedBy { get; set; }
        public TimestampDto CreatedDate { get; set; }
        public int ModifiedBy { get; set; }
        public TimestampDto ModifiedDate { get; set; }
        public bool IsDeleted { get; set; }
        public double UnitPrice { get; set; }
    }

    public class TimestampDto
    {
        public long Seconds { get; set; }
        public int Nanos { get; set; }
    }
}
