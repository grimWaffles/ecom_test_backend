using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Helpers.Converters;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using System.Data;

namespace OrderServiceGrpc.Services
{
    public interface ICheckoutService
    {
        Task<(bool, CartDto?)> CheckoutAsync(int cartId, int userId);
        Task<(bool, CartDto?)> CheckoutCartAsync(int cartId, int userId);
    }

    public class CheckoutService : ICheckoutService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CheckoutService> _logger;
        private readonly IOrderService _orderService;

        private readonly IConfiguration _config;
        private readonly int ReservationMinutes;

        public CheckoutService(ILogger<CheckoutService> logger, AppDbContext dbContext, IConfiguration config, IOrderService orderService)
        {
            _logger = logger; _context = dbContext; _orderService = orderService; _config = config;
            ReservationMinutes = config.GetSection("InventoryLockInMinutes") != null ? Convert.ToInt32(config.GetSection("InventoryLockInMinutes").Value) : 10;
        }

        public async Task<(bool, CartDto?)> CheckoutAsync(int cartId, int userId)
        {
            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Load cart with tracking, only the items still in play (Processing/Reserved)
                Cart? cart = await _context.Carts
                    
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

        public async Task<(bool, CartDto?)> CheckoutCartAsync(int cartId, int userId)
        {
            (bool status, CartDto? result) = await ProcessUserCart(cartId, userId);

            if (!status)
            {
                return (status, result);
            }

            //Call create order

            return (status, result);
        }

        private async Task<(bool, CartDto?)> ProcessUserCart(int cartId, int userId)
        {
            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                Cart? cart = await _context.Carts
                    
                    .Include(c => c.Items.Where(i => i.StatusId == CartItemStatusIds.Processing
                                                  || i.StatusId == CartItemStatusIds.Reserved))
                    .FirstOrDefaultAsync(c => c.Id == cartId && c.UserId == userId); // confirm the right ownership field

                if (cart == null || cart.Items.Count == 0)
                {
                    _logger.LogWarning("Checkout failed: cart not found or empty. CartId: {cartId}, UserId: {userId}", cartId, userId);
                    return (false, null);
                }

                List<int> productIds = cart.Items.Select(x => x.ProductId).ToList();

                List<Inventory> inventories = await _context.Inventory
                    .Where(iv => productIds.Contains(iv.ProductId) && !iv.IsDeleted)
                    .ToListAsync();

                Dictionary<int, int> unavailableQuantities = new Dictionary<int, int>();

                DateTime currentDateTime = DateTime.UtcNow;

                foreach (CartItem item in cart.Items)
                {
                    Inventory? inventory = inventories.Where(x => x.ProductId == item.ProductId).FirstOrDefault();

                    int availableQuantity = inventory != null ? inventory.Quantity - inventory.ReservedQuantity : 0;

                    if (inventory != null && item.ReservationExpiresAt < currentDateTime && item.Quantity <= availableQuantity)
                    {
                        //Reserve Item
                        item.StatusId = CartItemStatusIds.Reserved;
                        item.ReservationExpiresAt = currentDateTime.AddMinutes(15);

                        //Update Inventory
                        inventory.ReservedQuantity += item.Quantity;
                        inventory.ModifiedBy = userId;
                        inventory.ModifiedDate = currentDateTime;
                    }
                    else
                    {
                        //mark as unavailable
                        item.StatusId = CartItemStatusIds.Unavailable;
                        unavailableQuantities.Add(item.ProductId, availableQuantity);
                    }

                    item.UpdatedAt = currentDateTime;
                    item.UpdatedBy = userId;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                CartDto dtoToReturn = CartMappingExtensions.ToDto(cart);

                dtoToReturn.Items.ForEach(x =>
                {
                    unavailableQuantities.TryGetValue(x.ProductId, out int quantity);

                    x.UnavailableQuantity = x.StatusId == CartItemStatusIds.Unavailable ? x.Quantity - quantity : 0;
                });

                return (true, dtoToReturn);

                //Dictionary<int, int> requested = cart.Items
                //    .GroupBy(i => i.ProductId)
                //    .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

                //List<int> productIds = requested.Keys.ToList();

                //Dictionary<int, Inventory> inventories = await _context.Inventory
                //    
                //    .Where(inv => productIds.Contains(inv.ProductId) && !inv.IsDeleted)
                //    .ToDictionaryAsync(inv => inv.ProductId);

                //// Check every requested product; missing row = 0 available
                //Dictionary<int, int> unavailableQuantities = new();

                //foreach ((int productId, int qty) in requested)
                //{
                //    int available = inventories.TryGetValue(productId, out Inventory? inv) ? inv.Quantity - inv.ReservedQuantity : 0;

                //    if (available < qty)
                //        unavailableQuantities[productId] = qty - available;
                //}

                //bool allItemsAvailable = unavailableQuantities.Count == 0;
                //DateTime now = DateTime.UtcNow;

                //if (allItemsAvailable)
                //{
                //    foreach ((int productId, int qty) in requested)
                //    {
                //        Inventory inv = inventories[productId];
                //        inv.ReservedQuantity += qty;
                //        inv.ModifiedBy = userId;
                //        inv.ModifiedDate = now;
                //    }

                //    DateTime expiresAt = now.AddMinutes(10);
                //    foreach (CartItem item in cart.Items)
                //    {
                //        item.ReservationExpiresAt = expiresAt;
                //        item.StatusId = CartItemStatusIds.Reserved;
                //        item.UpdatedAt = now;
                //        item.UpdatedBy = userId;
                //    }

                //    cart.StatusId = CartStatusIds.CheckedOut;
                //    cart.UpdatedAt = now;
                //    cart.UpdatedBy = userId;

                //    await _context.SaveChangesAsync();
                //}

                //await transaction.CommitAsync();

                //CartDto dto = CartMappingExtensions.ToDto(cart); // mapped after changes

                //if (!allItemsAvailable)
                //{
                //    foreach (CartItemDto x in dto.Items.Where(x => unavailableQuantities.ContainsKey(x.ProductId)))
                //    {
                //        x.UnavailableQuantity = unavailableQuantities[x.ProductId];
                //        x.StatusId = CartItemStatusIds.Unavailable;
                //    }
                //}

                //return (allItemsAvailable, dto);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to checkout cart {cartId} for user {userId}", cartId, userId);
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<(bool AllReserved, CartDto? Cart)> ProcessUserCart2(int cartId, int userId)
        {
            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                Cart? cart = await _context.Carts
                    
                    .Include(c => c.Items.Where(i => i.StatusId == CartItemStatusIds.Processing
                                                  || i.StatusId == CartItemStatusIds.Reserved))
                    .FirstOrDefaultAsync(c => c.Id == cartId && c.UserId == userId);

                if (cart == null || cart.Items.Count == 0)
                {
                    _logger.LogWarning("Checkout failed: cart not found or empty. CartId: {cartId}, UserId: {userId}", cartId, userId);
                    return (false, null);
                }

                DateTime now = DateTime.UtcNow;
                List<int> productIds = cart.Items.Select(x => x.ProductId).Distinct().ToList();

                Dictionary<int, Inventory> inventories = await _context.Inventory
                    
                    .Where(iv => productIds.Contains(iv.ProductId) && !iv.IsDeleted)
                    .ToDictionaryAsync(iv => iv.ProductId);

                bool allReserved = true;

                foreach (CartItem item in cart.Items)
                {
                    inventories.TryGetValue(item.ProductId, out Inventory? inventory);

                    if (item.StatusId == CartItemStatusIds.Reserved)
                    {
                        // Still-valid reservation: leave it completely alone
                        if (item?.ReservationExpiresAt != null && item.ReservationExpiresAt > now)
                            continue;

                        // Expired but still marked Reserved: release the old hold first
                        // (safe only if your cleanup job also changes the status when it releases)
                        if (inventory != null)
                            inventory.ReservedQuantity -= item.Quantity;
                    }

                    if (inventory != null && item.Quantity <= inventory.Quantity - inventory.ReservedQuantity)
                    {
                        item.StatusId = CartItemStatusIds.Reserved;
                        item.ReservationExpiresAt = now.AddMinutes(ReservationMinutes);

                        inventory.ReservedQuantity += item.Quantity;
                        inventory.ModifiedBy = userId;
                        inventory.ModifiedDate = now;
                    }
                    else
                    {
                        item.StatusId = CartItemStatusIds.Unavailable;
                        allReserved = false;
                    }

                    item.UpdatedAt = now;
                    item.UpdatedBy = userId;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (allReserved, CartMappingExtensions.ToDto(cart));
            }
            catch (DbUpdateConcurrencyException e)   // requires RowVersion on Inventory
            {
                _logger.LogWarning(e, "Concurrent inventory update while checking out cart {cartId}", cartId);
                return (false, null);                // or retry
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to checkout cart {cartId} for user {userId}", cartId, userId);
                throw;                               // dispose rolls back
            }
        }
    }
}
