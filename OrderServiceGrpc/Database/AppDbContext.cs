using Microsoft.EntityFrameworkCore;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Database
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<OrderOutbox> OrderOutbox { get; set; }
        public DbSet<OutboxStatus> OutboxStatus { get; set; }

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
        }
    }
}
