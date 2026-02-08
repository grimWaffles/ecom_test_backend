using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata.Ecma335;

namespace UserServiceGrpc.Models.Entities
{
    [Table("RoleAccess")]
    public class RoleAccess
    {
        public RoleAccess()
        {
            
        }
        public int RoleId { get; set; }

        [Column(TypeName ="varchar(40)")]
        public string RoleDetail { get; set; }

        [Column(TypeName = "varchar(40)")]
        public string ModuleName { get; set; }

        [Column(TypeName = "varchar(40)")]
        public string FrmDetail { get; set; }

        public bool ViewPermission { get; set; }
        public bool AddPermission { get; set; }
        public bool EditPermission { get; set; }
        public bool DeletePermission { get; set; }
    }
}
