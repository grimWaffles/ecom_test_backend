using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;


namespace UserServiceGrpc.Models.Entities
{
    [Table("Roles")]
    [Index(nameof(Name), IsUnique = true)]
    public class Role
    {
        public Role()
        {

        }

        [Key]
        public int Id { get; set; }

        [MaxLength(40)]
        public string Name { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        public int CreatedBy { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public int? ModifiedBy { get; set; }

        public bool IsDeleted { get; set; } = false;

        //Foreign Keys
        [ForeignKey(nameof(CreatedBy))]
        [DeleteBehavior(DeleteBehavior.ClientNoAction)]
        public virtual UserModel CreatedByUser { get; set; }

        [ForeignKey(nameof(ModifiedBy))]
        [DeleteBehavior(DeleteBehavior.ClientNoAction)]
        public virtual UserModel ModifiedByUser { get; set; }

        public virtual ICollection<UserModel> Users { get; set; }
    }
}
