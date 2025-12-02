using Microsoft.EntityFrameworkCore;
using static UserServiceGrpc.Helpers.Enums;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Database
{
    public class AppDbContext : DbContext
    {
        public DbSet<UserModel> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        
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

            modelBuilder.Entity<Role>().Property(r => r.Name).HasConversion(r => r.ToString(), r => (UserRole)Enum.Parse(typeof(UserRole), r));
        }
    }
}
