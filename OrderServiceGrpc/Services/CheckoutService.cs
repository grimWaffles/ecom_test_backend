using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Helpers.Converters;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Services
{
    public interface ICheckoutService
    {
        public Task<(bool, CartDto?)> CheckoutAsync(int cartId, int userId);
    }

    public class CheckoutService : ICheckoutService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CheckoutService> _logger;
        private readonly ICartService _cartService;
        private readonly IInventoryService _inventoryService;

        public CheckoutService(ILogger<CheckoutService> logger, AppDbContext dbContext, ICartService cartService, IInventoryService inventoryService)
        {
            _logger = logger; _cartService = cartService; _inventoryService = inventoryService; _context = dbContext;
        }

        public async Task<(bool, CartDto?)> CheckoutAsync(int cartId, int userId)
        {
            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Load cart with tracking, only the items still in play (Processing/Reserved)
                Cart? cart = await _context.Carts
                    .WithTracking(track: true)
                    .Include(c => c.Items.Where(i => i.StatusId == CartItemStatusIds.Processing || i.StatusId == CartItemStatusIds.Reserved))
                    .FirstOrDefaultAsync(c => c.Id == cartId && c.UserId == userId);

                if (cart == null || cart.Items.Count == 0)
                {
                    _logger.LogWarning("Checkout failed: cart not found or has no active items. CartId: {cartId}, UserId: {userId}", cartId, userId);
                    await transaction.RollbackAsync();
                    return (false, null);
                }

                // Sum requested quantity by product
                Dictionary<int, int> requestedQuantities = cart.Items
                    .GroupBy(i => i.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

                List<int> productIds = requestedQuantities.Keys.ToList();

                // Load matching inventory rows, tracked, so we can mutate ReservedQuantity
                List<Inventory> inventories = await _context.Inventory
                    .WithTracking(track: true)
                    .Where(inv => productIds.Contains(inv.ProductId) && !inv.IsDeleted)
                    .ToListAsync();

                bool allAvailable = requestedQuantities.All(rq =>
                {
                    Inventory? inv = inventories.FirstOrDefault(i => i.ProductId == rq.Key);
                    return inv != null && (inv.Quantity - inv.ReservedQuantity) >= rq.Value;
                });

                if (allAvailable)
                {
                    DateTime now = DateTime.UtcNow;
                    DateTime reservationExpiresAt = now.AddMinutes(5);

                    foreach (KeyValuePair<int, int> kvp in requestedQuantities)
                    {
                        Inventory inv = inventories.First(i => i.ProductId == kvp.Key);
                        inv.ReservedQuantity += kvp.Value;
                        inv.ModifiedBy = userId;
                        inv.ModifiedDate = now;
                    }

                    foreach (CartItem item in cart.Items)
                    {
                        item.ReservationExpiresAt = reservationExpiresAt;
                        item.StatusId = CartItemStatusIds.Reserved;
                        item.UpdatedAt = now;
                        item.UpdatedBy = userId;
                    }
                }
                else
                {
                    DateTime now = DateTime.UtcNow;

                    foreach (CartItem item in cart.Items)
                    {
                        item.Quantity = 0;
                        item.StatusId = CartItemStatusIds.Unavailable;
                        item.UpdatedAt = now;
                        item.UpdatedBy = userId;
                    }

                    _logger.LogWarning("Checkout failed: insufficient inventory. CartId: {cartId}, UserId: {userId}", cartId, userId);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                CartDto cartDto = CartMappingExtensions.ToDto(cart);

                return (allAvailable, cartDto);
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                _logger.LogError(e, "Failed to checkout cart with cartID: {cId} for UserId: {uId}", cartId, userId);
                return (false, null);
            }
        }
    }
}
