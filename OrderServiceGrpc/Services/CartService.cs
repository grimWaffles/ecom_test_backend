using Grpc.Core;
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
        public Task<CartDto> GetCartByCartId(int cartId, int statusId = CartStatusIds.Active);
        public Task<CartDto> GetCartByUserId(int userId, int statusId = CartStatusIds.Active);
        public Task<(bool, string, Cart?)> ModifyCart(SaveCartDto cart, int userId);
    }

    public class CartService : ICartService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CartService> _logger;

        public CartService(AppDbContext database, ILogger<CartService> logger)
        {
            // FIX: guard against null dependencies (fails fast at construction instead of a NullReferenceException later)
            _context = database ?? throw new ArgumentNullException(nameof(database));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PagedCartResult> GetAllCarts(int pageNumber, int pageSize, int statusId = CartStatusIds.Active)
        {
            if (pageNumber < 1 || pageSize < 1 || statusId < 1)
            {
                _logger.LogWarning(
                    "Invalid parameters for GetAllCarts. PageNumber: {pageNumber}, PageSize: {pageSize}, StatusId: {statusId}",
                    pageNumber, pageSize, statusId);
                return null;
            }

            try
            {
                IQueryable<Cart> baseQuery = _context.Carts.AsNoTracking()
                    .Where(c => c.StatusId == statusId)
                    .OrderByDescending(c => c.CreatedAt);

                // FIX: count the full matching set BEFORE paging, not after Skip/Take (was undercounting to at most pageSize)
                int totalCount = await baseQuery.CountAsync();

                List<CartDto> list = await baseQuery
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => CartMappingExtensions.ToDto(c))
                    .ToListAsync();

                PagedCartResult result = new PagedCartResult()
                {
                    Page = pageNumber,
                    PageSize = pageSize,
                    Items = list,
                    TotalCount = totalCount,
                    // FIX: cast to decimal BEFORE dividing so Ceiling has something meaningful to round;
                    // also guard divide-by-zero-shaped edge case when totalCount is 0
                    TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((decimal)totalCount / pageSize)
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching cart list. PageNumber: {pageNumber}, PageSize: {pageSize}, StatusId: {statusId}",
                    pageNumber, pageSize, statusId);
                throw;
            }
        }

        public async Task<CartDto> GetCartByCartId(int cartId, int statusId = CartStatusIds.Active)
        {
            // FIX: added validation (previously none)
            if (cartId < 1 || statusId < 1)
            {
                _logger.LogWarning("Invalid parameters for GetCartByCartId. CartId: {cartId}, StatusId: {statusId}", cartId, statusId);
                return new CartDto();
            }

            try
            {
                IQueryable<Cart> cartQuery = _context.Carts.AsNoTracking()
                    .Include(c => c.Status)
                    // FIX: filter must be applied to the Items collection itself (filtered Include),
                    // not chained after ThenInclude — the old .Where(i => ...) was filtering Cart, not CartItem
                    .Include(c => c.Items.Where(i => i.StatusId == CartItemStatusIds.Processing || i.StatusId == CartItemStatusIds.Reserved))
                        .ThenInclude(i => i.Status)
                    .Where(c => c.Id == cartId && c.StatusId == statusId);

                List<CartDto> cartDtos = await cartQuery.Select(c => CartMappingExtensions.ToDto(c)).ToListAsync();

                // FIX: Select().ToListAsync() never returns null, it returns an empty list — check Count instead,
                // otherwise cartDtos[0] below would throw IndexOutOfRangeException on a genuine "not found"
                if (cartDtos.Count == 0)
                {
                    _logger.LogWarning("Cart not found with Id: {cartId}", cartId);
                    return new CartDto();
                }

                return cartDtos[0];
            }
            catch (Exception e)
            {
                // FIX: pass the exception itself so stack trace/details are actually captured
                _logger.LogError(e, "Failed to fetch cart with Id: {cartId}", cartId);
                throw;
            }
        }

        public async Task<CartDto> GetCartByUserId(int userId, int statusId = CartStatusIds.Active)
        {
            // FIX: added validation (previously none)
            if (userId < 1 || statusId < 1)
            {
                _logger.LogWarning("Invalid parameters for GetCartByUserId. UserId: {userId}, StatusId: {statusId}", userId, statusId);
                return new CartDto();
            }

            try
            {
                IQueryable<Cart> cartQuery = _context.Carts.AsNoTracking()
                    .Include(c => c.Status)
                    // FIX: same filtered-Include correction as GetCartByCartId
                    .Include(c => c.Items.Where(i => i.StatusId == CartItemStatusIds.Processing || i.StatusId == CartItemStatusIds.Reserved))
                        .ThenInclude(i => i.Status)
                    .Where(c => c.UserId == userId && c.StatusId == statusId);

                List<CartDto> cartDtos = await cartQuery.Select(c => CartMappingExtensions.ToDto(c)).ToListAsync();

                // FIX: same null-vs-empty correction as GetCartByCartId
                if (cartDtos.Count == 0)
                {
                    _logger.LogWarning("Cart not found with UserId: {userId}", userId);
                    return new CartDto();
                }

                return cartDtos[0];
            }
            catch (Exception e)
            {
                // FIX: pass the exception itself so stack trace/details are actually captured
                _logger.LogError(e, "Failed to fetch cart with UserId: {userId}", userId);
                throw;
            }
        }

        public async Task<(bool, string, Cart?)> ModifyCart(SaveCartDto cartDto, int userId)
        {
            // FIX: validate inputs before mapping/using them (previously none at all)
            if (cartDto == null)
            {
                _logger.LogWarning("ModifyCart called with null cartDto. UserId: {userId}", userId);
                return (false, "Cart data is required", null);
            }

            if (userId < 1)
            {
                _logger.LogWarning("ModifyCart called with invalid UserId: {userId}", userId);
                return (false, "Invalid user", null);
            }

            Cart cart = CartMappingExtensions.SaveCartDtoToEntity(cartDto);
            cart.Items ??= new List<CartItem>();

            // FIX: validate item data — previously a bad ProductId or negative Quantity would flow straight to the DB
            if (cart.Items.Any(i => i.ProductId <= 0 || i.Quantity < 0))
            {
                _logger.LogWarning("ModifyCart received invalid item data for CartId: {cartId}, UserId: {userId}", cart.Id, userId);
                return (false, "Invalid item data: ProductId must be positive and Quantity cannot be negative", null);
            }

            // FIX: reject duplicate ProductIds in the incoming list — the diffing logic in UpdateCartAsync
            // assumes at most one entry per ProductId and silently misbehaves otherwise
            if (cart.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
            {
                _logger.LogWarning("ModifyCart received duplicate ProductId entries for CartId: {cartId}, UserId: {userId}", cart.Id, userId);
                return (false, "Duplicate ProductId entries are not allowed", null);
            }

            (bool, string, Cart?) result = (cart.Id, cart.Items.Count) switch
            {
                (0, > 0) => await InsertCartAsync(cart, userId),
                ( > 0, > 0) => await UpdateCartAsync(cart, userId),
                ( > 0, 0) => await DeleteCartAsync(cart, userId),
                (_, _) => (false, "Failed to perform action", null)
            };

            // FIX: log only on failure, at the point where we know the outcome
            if (!result.Item1)
            {
                _logger.LogWarning("ModifyCart failed for CartId: {cartId}, UserId: {userId}. Reason: {reason}", cart.Id, userId, result.Item2);
            }

            return result;
        }

        private async Task<(bool, string, Cart?)> InsertCartAsync(Cart cart, int userId)
        {
            try
            {
                cart.CreatedAt = DateTime.UtcNow;
                cart.CreatedBy = userId;
                cart.StatusId = CartStatusIds.Active;
                cart.UserId = userId;

                foreach (CartItem i in cart.Items)
                {
                    i.StatusId = CartItemStatusIds.Processing;
                    i.CreatedAt = DateTime.UtcNow;
                    i.CreatedBy = userId;
                }

                var res = await _context.Carts.AddAsync(cart);
                await _context.SaveChangesAsync();

                return (true, "Cart created successfully", res.Entity);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to add cart for UserId: {userId}", userId);
                return (false, "Failed to add cart", null);
            }
        }

        private async Task<(bool, string, Cart?)> UpdateCartAsync(Cart cart, int userId)
        {
            try
            {
                // FIX: FirstAsync throws if no match — use FirstOrDefaultAsync and handle "not found" explicitly
                Cart cartFromDb = await _context.Carts.Where(c => c.Id == cart.Id).Include(c => c.Items).FirstOrDefaultAsync();

                if (cartFromDb == null)
                {
                    _logger.LogWarning("UpdateCartAsync failed: cart not found. CartId: {cartId}, UserId: {userId}", cart.Id, userId);
                    return (false, "Cart not found", null);
                }

                List<CartItem> deleteList = cartFromDb.Items.Where(i => !cart.Items.Any(ci => i.ProductId == ci.ProductId)).ToList();
                List<CartItem> addList = cart.Items.Where(ci => !cartFromDb.Items.Any(i => i.ProductId == ci.ProductId)).ToList();
                List<CartItem> updateList = cartFromDb.Items.Where(ci => cart.Items.Any(i => i.ProductId == ci.ProductId && i.Quantity != ci.Quantity)).ToList();

                if (deleteList.Count == 0 && addList.Count == 0 && updateList.Count == 0)
                {
                    return (true, "Update successful", cartFromDb);
                }

                // Process the deleted list — mutating in place is enough since these are entities already
                // tracked as part of cartFromDb.Items
                deleteList.ForEach(x =>
                {
                    x.IsDeleted = true; x.UpdatedAt = DateTime.UtcNow; x.UpdatedBy = userId; x.StatusId = CartItemStatusIds.Removed;
                });

                // Process the updateList — same reasoning, these are shared references into cartFromDb.Items
                updateList.ForEach(x =>
                {
                    x.UpdatedAt = DateTime.UtcNow; x.UpdatedBy = userId;
                    x.Quantity = cart.Items.First(ci => ci.ProductId == x.ProductId).Quantity;
                });

                // Process the addList
                addList.ForEach(i =>
                {
                    i.StatusId = CartItemStatusIds.Processing;
                    i.CreatedAt = DateTime.UtcNow;
                    i.CreatedBy = userId;
                    i.IsDeleted = false;
                });

                // FIX (from earlier bug): only append the genuinely new items instead of rebuilding
                // Items from deleteList+addList+updateList, which silently dropped unchanged items
                cartFromDb.Items = cartFromDb.Items.Concat(addList).ToList();

                await _context.SaveChangesAsync();

                return (true, "Update successful", cartFromDb);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to update cart. CartId: {cartId}, UserId: {userId}", cart.Id, userId);
                return (false, "Failed to update cart", null);
            }
        }

        private async Task<(bool, string, Cart?)> DeleteCartAsync(Cart cart, int userId)
        {
            try
            {
                // FIX: FirstAsync throws instead of returning null — use FirstOrDefaultAsync so the null check below is reachable
                Cart cartFromDb = await _context.Carts.Where(c => c.Id == cart.Id).Include(c => c.Items).FirstOrDefaultAsync();

                if (cartFromDb == null)
                {
                    _logger.LogWarning("DeleteCartAsync failed: cart not found. CartId: {cartId}, UserId: {userId}", cart.Id, userId);
                    return (false, "Cart not found", null);
                }

                foreach (CartItem item in cartFromDb.Items)
                {
                    item.UpdatedAt = DateTime.UtcNow; item.UpdatedBy = userId; item.IsDeleted = true; item.StatusId = CartItemStatusIds.Removed;
                }

                cartFromDb.UpdatedAt = DateTime.UtcNow; cartFromDb.UpdatedBy = userId; cartFromDb.IsDeleted = true; cartFromDb.StatusId = CartStatusIds.Abandoned;

                // FIX: this is the big one — SaveChangesAsync was never called, and the method unconditionally
                // returned (false, "Failed to delete cart", null) even when everything succeeded
                await _context.SaveChangesAsync();

                return (true, "Cart deleted successfully", cartFromDb);
            }
            catch (Exception e)
            {
                // FIX: use LogError (not LogWarning) for an actual exception, and include full context
                _logger.LogError(e, "Failed to delete cart. CartId: {cartId}, UserId: {userId}", cart.Id, userId);
                return (false, "Failed to delete cart", null);
            }
        }

    }
}
