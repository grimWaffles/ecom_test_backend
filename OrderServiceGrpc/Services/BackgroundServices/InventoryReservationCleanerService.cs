using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using System.Data;

namespace OrderServiceGrpc.Services.BackgroundServices
{
    public class InventoryReservationCleanerService : BackgroundService
    {
        private readonly ILogger<InventoryReservationCleanerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly int SystemUserId = 1;
        private const int DelayInSeconds = 6000;

        public InventoryReservationCleanerService(ILogger<InventoryReservationCleanerService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(DelayInSeconds, stoppingToken);
                    await CleanInventoryReservations(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("OutboxExecutor is stopping due to cancellation request.");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred in OutboxExecutor.");
                    throw;
                }
            }
        }

        private async Task CleanInventoryReservations(CancellationToken cancellationToken)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            AppDbContext _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                DateTime now = DateTime.UtcNow;

                List<CartItem> expiredItems = await _context.CartItems
                    .Where(ci => !ci.IsDeleted
                              && ci.StatusId == CartItemStatusIds.Reserved
                              && ci.ReservationExpiresAt < now)
                    .ToListAsync(cancellationToken);

                if (expiredItems.Count == 0)
                    return;

                foreach (CartItem item in expiredItems)
                {
                    item.StatusId = CartItemStatusIds.Processing;
                    item.UpdatedAt = now;
                    item.UpdatedBy = SystemUserId;
                }

                Dictionary<int, int> toRelease = expiredItems
                    .GroupBy(x => x.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

                List<Inventory> inventories = await _context.Inventory
                    .Where(inv => !inv.IsDeleted && toRelease.Keys.Contains(inv.ProductId))
                    .ToListAsync(cancellationToken);

                foreach (Inventory inv in inventories)
                {
                    int release = toRelease[inv.ProductId];

                    if (inv.ReservedQuantity < release)
                        _logger.LogWarning("ReservedQuantity drift for product {productId}: reserved {reserved}, releasing {release}",
                            inv.ProductId, inv.ReservedQuantity, release);

                    inv.ReservedQuantity = Math.Max(0, inv.ReservedQuantity - release);
                    inv.ModifiedBy = SystemUserId;
                    inv.ModifiedDate = now;
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Reservation cleanup cancelled");
            }
            catch (DbUpdateConcurrencyException e)
            {
                _logger.LogWarning(e, "Concurrent update during reservation cleanup; will retry next cycle");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Reservation cleanup failed");
            }
        }
    }
}
