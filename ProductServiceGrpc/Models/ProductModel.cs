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
    public class ProductModel : BaseModel 
    {
        public ProductModel()
        {
            Name = ""; DefaultQuantity = 0; Rating = 0; Price = 0; Description = "";
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; }

        public int? DefaultQuantity { get; set; }

        [Precision(18, 4)]
        public decimal Rating { get; set; }

        [Precision(18, 4)]
        public decimal Price { get; set; }

        [MaxLength(300)]
        public string? Description { get; set; }

        [Required]
        public int SellerId { get; set; }

        [Required]
        public int ProductCategoryId { get; set; }

        public virtual SellerModel Seller { get; set; }

        public virtual ProductCategoryModel ProductCategory { get; set; }

        [NotMapped]
        public string SellerCompanyName { get; set; }

        [NotMapped]
        public string ProductCategoryName { get; set; }
    }
}
