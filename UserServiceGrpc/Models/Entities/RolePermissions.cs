using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserServiceGrpc.Models.Entities
{
    [Table("RolePermissions")]
    public class RolePermissions
    {
        public RolePermissions()
        {
        }

        // Primary Key
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Foreign key from Roles table
        [Required]
        public int RoleId { get; set; }

        // Not null
        [Required]
        [MaxLength(255)]
        public string ApiPath { get; set; }

        // Default false (0) and not null
        [Required]
        public bool ViewPermission { get; set; } = false;

        [Required]
        public bool AddPermission { get; set; } = false;

        [Required]
        public bool EditPermission { get; set; } = false;

        [Required]
        public bool DeletePermission { get; set; } = false;

        // CreatedBy (FK to Users.Id) - not null
        [Required]
        public int CreatedBy { get; set; }

        // ModifiedBy (FK to Users.Id) - nullable
        public int? ModifiedBy { get; set; }

        // CreatedDate - not null
        [Required]
        public DateTime CreatedDate { get; set; }

        // ModifiedDate - nullable
        public DateTime? ModifiedDate { get; set; }

        // Optional navigation properties (recommended)
        [ForeignKey(nameof(RoleId))]
        public virtual Role Role { get; set; }

        [ForeignKey(nameof(CreatedBy))]
        public virtual UserModel CreatedByUser { get; set; }

        [ForeignKey(nameof(ModifiedBy))]
        public virtual UserModel ModifiedByUser { get; set; }
    }
}