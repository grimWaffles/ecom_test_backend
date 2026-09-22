using Microsoft.EntityFrameworkCore;
using OrderServiceGrpc.Models.Entities;
using System.Reflection.Emit;

namespace OrderServiceGrpc.Database
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<OrderOutbox> OrderOutbox { get; set; }
        public DbSet<OutboxStatus> OutboxStatus { get; set; }
        public DbSet<Inventory> Inventory { get; set; }
        public DbSet<CartStatus> CartStatuses { get; set; }
        public DbSet<CartItemStatus> CartItemStatuses { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Payload JSON check constraint — mirrors the SQL schema
            modelBuilder.Entity<OrderOutbox>()
                .ToTable(tb => tb.HasCheckConstraint("CK_OrderOutbox_Payload_JSON", "ISJSON(Payload) > 0"));

            // Headers default
            modelBuilder.Entity<OrderOutbox>()
                .Property(o => o.Headers)
                .HasDefaultValue("{}");

            // Seed OutboxStatus
            modelBuilder.Entity<OutboxStatus>().HasData(
                new OutboxStatus { StatusId = 1, StatusName = "PENDING" },
                new OutboxStatus { StatusId = 2, StatusName = "PROCESSING" },
                new OutboxStatus { StatusId = 3, StatusName = "PUBLISHED" },
                new OutboxStatus { StatusId = 4, StatusName = "FAILED" }
            );

            // Partial index equivalent — EF Core filtered index
            modelBuilder.Entity<OrderOutbox>()
                .HasIndex(o => new { o.StatusId, o.ScheduledAt })
                .HasFilter("StatusId IN (1, 4)")
                .HasDatabaseName("idx_OrderOutbox_Status_Scheduled");

            modelBuilder.Entity<OrderOutbox>()
                .HasIndex(o => new { o.AggregateId, o.AggregateType })
                .HasDatabaseName("idx_OrderOutbox_Aggregate");

            // CART & CART ITEMS
            modelBuilder.Entity<CartStatus>(e =>
            {
                e.HasIndex(x => x.CartStatusName).IsUnique();
            });

            modelBuilder.Entity<CartItemStatus>(e =>
            {
                e.HasIndex(x => x.CartItemStatusName).IsUnique();
            });

            modelBuilder.Entity<Cart>(e =>
            {
                e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                e.Property(x => x.IsDeleted).HasDefaultValue(false);

                e.HasOne(x => x.Status)
                    .WithMany(s => s.Carts)
                    .HasForeignKey(x => x.StatusId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.UserId);
                e.HasQueryFilter(x => !x.IsDeleted); // soft delete
            });

            modelBuilder.Entity<CartItem>(e =>
            {
                e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                e.Property(x => x.IsDeleted).HasDefaultValue(false);

                e.HasOne(x => x.Cart)
                    .WithMany(c => c.Items)
                    .HasForeignKey(x => x.CartId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Status)
                    .WithMany(s => s.CartItems)
                    .HasForeignKey(x => x.StatusId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.CartId);
                e.HasIndex(x => x.ProductId);
                e.HasQueryFilter(x => !x.IsDeleted); // soft delete
            });
        }
    }
}
