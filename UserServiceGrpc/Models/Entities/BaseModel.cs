using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Models
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

        [ForeignKey(nameof(CreatedBy))]
        public virtual UserModel CreatedByUser { get; set; }

        [ForeignKey(nameof(ModifiedBy))]
        public virtual UserModel ModifiedByUser { get; set; }
    }
}
