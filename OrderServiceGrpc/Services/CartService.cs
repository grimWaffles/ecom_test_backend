using Microsoft.EntityFrameworkCore;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Helpers.Converters;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Services
{
    public interface ICartService
    {
        public Task<PagedCartResult> GetAllCarts(int pageNumber, int pageSize, int statusId = CartStatusIds.Active);
        public Task<CartDto> GetCartByCartId(int cartId, int statusId);
        public Task<CartDto> GetCartByUserId(int userId);
        public Task<CartDto> ModifyCart(SaveCartDto cart, int userId);
    }

    public class CartService : ICartService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CartService> _logger;

        public CartService(AppDbContext database, ILogger<CartService> logger)
        {
            _context = database;
            _logger = logger;
        }

        public async Task<PagedCartResult> GetAllCarts(int pageNumber, int pageSize, int statusId = CartStatusIds.Active)
        {
            try
            {
                if (pageNumber < 1 || pageSize < 1 || statusId < 1)
                {
                    _logger.LogWarning("Parameter are invalid. Failed to fetch cart list");
                    return null;
                }

                var query = _context.Carts.AsNoTracking()
                    .Where(c => c.StatusId == statusId)
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize);

                List<CartDto> list = await query.Select(c => CartMappingExtensions.ToDto(c)).ToListAsync();
                int totalCount = await query.CountAsync();

                PagedCartResult result = new PagedCartResult()
                {
                    Page = pageNumber,
                    PageSize = pageSize,
                    Items = list,
                    TotalCount = totalCount,
                    TotalPages = Convert.ToInt32(System.Math.Ceiling((decimal)(totalCount / pageSize)))
                };

                return result;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error fetching cart list. Trace: {trace}", ex.StackTrace);
                throw;
            }
        }

        public async Task<CartDto> GetCartByCartId(int cartId, int statusId = CartStatusIds.Active)
        {
            try
            {
                IQueryable<Cart> cart = _context.Carts.AsNoTracking()
                .Include(c => c.Status)
                .Include(c => c.Items)
                    .ThenInclude(i => i.Status)
                    .Where(i=> i.StatusId == CartItemStatusIds.Processing || i.StatusId == CartItemStatusIds.Reserved)
                .Where(c => c.Id == cartId && c.StatusId == statusId);

                List<CartDto> cartDtos = await cart.Select(c => CartMappingExtensions.ToDto(c)).ToListAsync();

                if (cartDtos == null)
                {
                    _logger.LogWarning("Cart not found with Id: {cartId}", cartId);
                    return new CartDto();
                }

                return cartDtos[0];
            }
            catch(Exception e)
            {
                _logger.LogError("Failed to fetch cart with Id: {cartId}", cartId);
                throw;
            }
        }

        public async Task<CartDto> GetCartByUserId(int userId)
        {
            throw new NotImplementedException();
        }

        public async Task<CartDto> ModifyCart(SaveCartDto cart, int userId)
        {
            throw new NotImplementedException();
        }
    }
}
