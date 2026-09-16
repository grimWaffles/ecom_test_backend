using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Confluent.Kafka.ConfigPropertyNames;

namespace OrderServiceGrpc.Models.Entities
{
    [Table("Inventory")]
    public class Inventory
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int ProductCategoryId { get; set; }

        [Required]
        public int Quantity { get; set; } = 0;

        [Required]
        public int CreatedBy { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; }

        public int? ModifiedBy { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}
