using ProductServiceGrpc.Models;
using Microsoft.EntityFrameworkCore;

namespace ProductServiceGrpc.Database
{
    public class AppDbContext : DbContext
    {
        public DbSet<ProductCategoryModel> ProductCategories {get; set;}
        public DbSet<SellerModel> Sellers { get; set; }
        public DbSet<ProductModel> Products { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductCategoryModel>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<ProductModel>().HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<SellerModel>().HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
