using System.ComponentModel.DataAnnotations;

namespace ProductServiceGrpc.Models
{
    public class BaseModel
    {
        [Required]
        public int CreatedBy { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public int? ModifiedBy { get; set; }

        [Required]
        public bool IsDeleted { get; set; } = false;
    }
}
