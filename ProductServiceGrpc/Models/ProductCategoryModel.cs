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
    [Index(nameof(CategoryName), IsUnique = true)]
    [Table("ProductCategories")]
    public class ProductCategoryModel : BaseModel
    {
        public ProductCategoryModel()
        {
            Products = new List<ProductModel>();
            CategoryName = "";
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(100)]
        public string CategoryName { get; set; }

        public virtual ICollection<ProductModel> Products { get; set; }
    }
}
