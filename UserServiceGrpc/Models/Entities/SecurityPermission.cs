using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserServiceGrpc.Models.Entities
{
    [Table("SecurityPermissions")]
    [Index(nameof(Permission),IsUnique=true)]
    public class SecurityPermission : BaseModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Permission { get; set; }


        //FKs and relationships
        public ICollection<Role> Roles { get; set; }
    }
}
