using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserServiceGrpc.Models.Entities
{
    [Table("RolePermissions")]
    public class RolePermissions : BaseModel
    {
        public RolePermissions() { }

        // Primary Key
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Foreign key from Roles table
        [Required]
        public int RoleId { get; set; }

        //Foreign key from Permissions table
        [Required]
        public int PermissionId { get; set; }


        //FKs and relationships
        [ForeignKey(nameof(RoleId))]
        public virtual Role Role { get; set; }

        [ForeignKey(nameof(PermissionId))]
        public virtual SecurityPermission Permission { get; set; }

    }
}