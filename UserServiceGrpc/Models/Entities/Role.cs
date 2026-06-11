using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;


namespace UserServiceGrpc.Models.Entities
{
    [Table("Roles")]
    [Index(nameof(Name), IsUnique = true)]
    public class Role
    {
        public Role() { }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(40)]
        public string Name { get; set; }

        //FKs and Relationships
        public virtual ICollection<UserModel> Users { get; set; }
        public ICollection<SecurityPermission> Permissions { get; set; }
    }
}
