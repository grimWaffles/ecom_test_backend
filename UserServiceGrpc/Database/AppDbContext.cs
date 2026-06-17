using Microsoft.EntityFrameworkCore;

using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Database
{
    public class AppDbContext : DbContext
    {
        public DbSet<UserModel> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<SecurityPermission> SecurityPermissions { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserModel>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<Role>().HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<UserModel>()
                .HasOne(x => x.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(f => f.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
