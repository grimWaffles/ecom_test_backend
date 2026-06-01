using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderServiceGrpc.Models.Entities
{
    [Table("OrderOutbox")]
    public class OrderOutbox
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int AggregateId { get; set; }

        [Required]
        [MaxLength(100)]
        public string AggregateType { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Topic { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string PartitionKey { get; set; } = string.Empty;

        [Required]
        public string Payload { get; set; } = string.Empty;

        [Required]
        public string Headers { get; set; } = "{}";

        [Required]
        public int StatusId { get; set; } = 1;

        [Required]
        public int RetryCount { get; set; } = 0;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAt { get; set; }

        [Required]
        public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;

        public string? ErrorMessage { get; set; }

        // Navigation
        [ForeignKey(nameof(StatusId))]
        public OutboxStatus Status { get; set; } = null!;
    }
}
