namespace API_Gateway.Models.Dtos
{
    public class InventoryReservationDto
    {
        public long Id { get; set; }
        public int ProductId { get; set; }
        public int CartId { get; set; }
        public int LockQuantity { get; set; }
        public DateTime LockExpirationDate { get; set; }

        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }

        public bool IsDeleted { get; set; }
    }

    public class InventoryReservationUpsertDto
    {
        public long Id { get; set; }
        public int ProductId { get; set; }
        public int CartId { get; set; }
        public int LockQuantity { get; set; }
        public DateTime LockExpirationDate { get; set; }
    }
}
