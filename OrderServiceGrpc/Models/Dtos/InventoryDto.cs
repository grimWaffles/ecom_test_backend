namespace OrderServiceGrpc.Models.Dtos
{
    public class InventoryDto
    {
        public long Id { get; set; }
        public int ProductId { get; set; }
        public int ProductCategoryId { get; set; }
        public int Quantity { get; set; }

        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public bool IsDeleted { get; set; }
    }

    public class InventoryUpsertDto
    {
        public long Id { get; set; }
        public int ProductId { get; set; }
        public int ProductCategoryId { get; set; }
        public int Quantity { get; set; }
    }
}
