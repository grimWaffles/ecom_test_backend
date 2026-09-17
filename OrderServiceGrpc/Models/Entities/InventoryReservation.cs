using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderServiceGrpc.Models.Entities
{
    [Table("InventoryReservation")]
    public class InventoryReservation
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public long CartId { get; set; }

        [Required]
        public int LockQuantity { get; set; }

        [Required]
        public DateTime LockExpirationDate { get; set; }

        [Required]
        public int CreatedBy { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}
