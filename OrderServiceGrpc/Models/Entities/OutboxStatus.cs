using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderServiceGrpc.Models.Entities
{
    [Table("OutboxStatus")]
    public class OutboxStatus
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int StatusId { get; set; }

        [Required]
        [MaxLength(20)]
        public string StatusName { get; set; } = string.Empty;

        // Navigation
        public ICollection<OrderOutbox> OrderOutboxes { get; set; } = new List<OrderOutbox>();
    }
}
