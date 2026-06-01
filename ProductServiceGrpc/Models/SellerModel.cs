using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ProductServiceGrpc.Models
{
    [Index(nameof(MobileNo), nameof(Email), IsUnique = true)]
    public class SellerModel : BaseModel
    {
        public SellerModel()
        {
            Products = new List<ProductModel>();
            CompanyName = "";
            Address = "";
            MobileNo = "";
            Email = "";
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(100)]
        public string CompanyName { get; set; }

        [MaxLength(100)]
        public string Address { get; set; }

        [MaxLength(11)]
        public string MobileNo { get; set; }

        [MaxLength(50)]
        public string Email { get; set; }

        [Precision(18, 2)]
        public decimal Rating { get; set; }

        public virtual ICollection<ProductModel> Products { get; set; }
    }
}
