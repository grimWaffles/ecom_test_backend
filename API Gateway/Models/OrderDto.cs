namespace API_Gateway.Models
{
    public class OrderDto
    {
        public int Id { get; set; }
        public DateTime? OrderDate { get; set; }
        public int OrderCounter { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal NetAmount { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class OrderItemDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal GrossAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool IsDeleted { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class OrderResponseDto
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public OrderDto? Order { get; set; }
    }

    public class OrderListResponseDto
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalPages { get; set; }
        public int TotalOrders { get; set; }
        public List<OrderDto> Orders { get; set; } = new();
    }
}
