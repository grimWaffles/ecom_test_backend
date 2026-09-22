namespace OrderServiceGrpc.Models.Dtos
{
    public static class CartStatusIds
    {
        public const int Active = 1;
        public const int CheckedOut = 2;
        public const int Abandoned = 3;
    }

    // Must match the seeded rows in CartItemStatus
    public static class CartItemStatusIds
    {
        public const int Reserved = 1; // not used for now
        public const int Expired = 2;
        public const int Unavailable = 3;
        public const int Removed = 4;
        public const int Purchased = 5;
        public const int Processing = 6;
    }

    public class CartStatusDto
    {
        public int Id { get; set; }
        public string CartStatusName { get; set; } = string.Empty;
    }

    public class CartItemStatusDto
    {
        public int Id { get; set; }
        public string CartItemStatusName { get; set; } = string.Empty;
    }

    public class CartItemDto
    {
        public int Id { get; set; }
        public int CartId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTime ReservationExpiresAt { get; set; }
        public int StatusId { get; set; }
        public string? StatusName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
    }

    // Full cart, including items
    public class CartDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int StatusId { get; set; }
        public string? StatusName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public List<CartItemDto> Items { get; set; } = new();
    }

    public class PagedCartResult
    {
        public List<CartDto> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    public class SaveCartDto
    {
        public int Id { get; set; }
        public List<SaveCartItemDto> Items { get; set; } = new();
    }

    // Price, status and reservation are set server-side, never by the client
    public class SaveCartItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
