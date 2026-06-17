using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserServiceGrpc.Models.Entities
{
    [Table("RolePermissions")]
    public class RolePermission : BaseModel
    {
        public RolePermission() { }

        // Primary Key
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        // Foreign key from Roles table
        [Required]
        public int RoleId { get; set; }

        //Foreign key from Permissions table
        [Required]
        public long PermissionId { get; set; }


        //FKs and relationships
        [ForeignKey(nameof(RoleId))]
        public virtual Role Role { get; set; }

        [ForeignKey(nameof(PermissionId))]
        public virtual SecurityPermission Permission { get; set; }
    }
}