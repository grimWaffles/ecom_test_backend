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
        Task<(bool, CartDto?)> CheckoutCartAsync(int cartId, int userId);
    }

    public class CheckoutService : ICheckoutService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CheckoutService> _logger;
        private readonly IOrderService _orderService;

        private readonly IConfiguration _config;
        private readonly int ReservationMinutes = 15;

        public CheckoutService(ILogger<CheckoutService> logger, AppDbContext dbContext, IConfiguration config, IOrderService orderService)
        {
            _logger = logger; _context = dbContext; _orderService = orderService; _config = config;
            ReservationMinutes = config.GetSection("InventoryLockInMinutes") != null ? Convert.ToInt32(config.GetSection("InventoryLockInMinutes").Value) : ReservationMinutes;
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
                    .Include(c => c.Items.Where(i => i.StatusId == CartItemStatusIds.Processing || i.StatusId == CartItemStatusIds.Reserved))
                    .FirstOrDefaultAsync(c => c.Id == cartId && c.UserId == userId); // confirm the right ownership field

                if (cart == null || cart.Items.Count == 0)
                {
                    _logger.LogWarning("Checkout failed: cart not found or empty. CartId: {cartId}, UserId: {userId}", cartId, userId);
                    return (false, null);
                }

                List<int> productIds = cart.Items.Select(x => x.ProductId).ToList();

                List<Inventory> inventories = await _context.Inventory.Where(iv => productIds.Contains(iv.ProductId) && !iv.IsDeleted).ToListAsync();

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
                        item.UnavailableQuantity = inventory!=null ? item.Quantity - availableQuantity : item.Quantity; 
                    }

                    item.UpdatedAt = currentDateTime;
                    item.UpdatedBy = userId;
                }

                CartDto dtoToReturn = CartMappingExtensions.ToDto(cart);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, dtoToReturn);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to checkout cart {cartId} for user {userId}", cartId, userId);
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
