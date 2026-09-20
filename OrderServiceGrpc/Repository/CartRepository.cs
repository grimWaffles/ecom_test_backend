using Microsoft.EntityFrameworkCore;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Repository
{
    public interface ICartRepository
    {
        Task<Cart?> GetByUserIdAndProductIdAsync(int userId, int productId);
        Task<List<Cart>> GetByUserIdAsync(int userId);
        Task<Cart?> GetByIdAsync(long id);
        Task<Cart> CreateAsync(Cart entity, int userId);
        Task<Cart?> UpdateAsync(Cart entity, int userId);
        Task<bool> DeleteAsync(long id, int userId);
    }
    public class CartRepository : ICartRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CartRepository> _logger;
        private readonly UnitOfWorkContext _uowContext;

        public CartRepository(
            AppDbContext context,
            ILogger<CartRepository> logger,
            UnitOfWorkContext uowContext)
        {
            _context = context;
            _logger = logger;
            _uowContext = uowContext;
        }

        public async Task<Cart?> GetByUserIdAndProductIdAsync(int userId, int productId)
        {
            _logger.LogInformation("Fetching Cart for UserId: {UserId}, ProductId: {ProductId}", userId, productId);
            try
            {
                Cart? record = await _context.Carts
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId && !x.IsDeleted);

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Cart for UserId: {UserId}, ProductId: {ProductId}", userId, productId);
                throw;
            }
        }

        public async Task<List<Cart>> GetByUserIdAsync(int userId)
        {
            _logger.LogInformation("Fetching Cart items for UserId: {UserId}", userId);
            try
            {
                IQueryable<Cart> query = _context.Carts
                    .AsNoTracking()
                    .Where(x => x.UserId == userId && !x.IsDeleted)
                    .OrderBy(x => x.Id);

                List<Cart> records = await query.ToListAsync();

                _logger.LogInformation("Retrieved {Count} Cart items for UserId: {UserId}", records.Count, userId);
                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Cart items for UserId: {UserId}", userId);
                throw;
            }
        }

        public async Task<Cart?> GetByIdAsync(long id)
        {
            _logger.LogInformation("Fetching Cart with Id: {Id}", id);
            try
            {
                Cart? record = await _context.Carts
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (record is null)
                    _logger.LogWarning("Cart with Id: {Id} not found", id);

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Cart with Id: {Id}", id);
                throw;
            }
        }

        public async Task<Cart> CreateAsync(Cart entity, int userId)
        {
            _logger.LogInformation("Creating Cart — UserId: {UserId}, ProductId: {ProductId}, Quantity: {Quantity}",
                entity.UserId, entity.ProductId, entity.Quantity);
            try
            {
                entity.CreatedBy = userId;
                entity.CreatedDate = DateTime.UtcNow;
                entity.IsDeleted = false;

                await _context.Carts.AddAsync(entity);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Created Cart with Id: {Id}", entity.Id);
                return entity;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating Cart — ProductId: {ProductId}", entity.ProductId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating Cart");
                throw;
            }
        }

        public async Task<Cart?> UpdateAsync(Cart entity, int userId)
        {
            _logger.LogInformation("Updating Cart with Id: {Id}", entity.Id);
            try
            {
                Cart? existing = await _context.Carts
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDeleted);

                if (existing is null)
                {
                    _logger.LogWarning("Cart with Id: {Id} not found for update", entity.Id);
                    return null;
                }

                existing.Quantity = entity.Quantity;
                existing.UnitPrice = entity.UnitPrice;

                if (!_uowContext.IsUnderUnitOfWork)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Updated Cart with Id: {Id}", existing.Id);
                return existing;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating Cart with Id: {Id}", entity.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating Cart with Id: {Id}", entity.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(long id, int userId)
        {
            _logger.LogInformation("Deleting Cart with Id: {Id}", id);
            try
            {
                Cart? existing = await _context.Carts
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (existing is null)
                {
                    _logger.LogWarning("Cart with Id: {Id} not found for delete", id);
                    return false;
                }

                existing.IsDeleted = true;

                if (!_uowContext.IsUnderUnitOfWork)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Deleted Cart with Id: {Id}", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting Cart with Id: {Id}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting Cart with Id: {Id}", id);
                throw;
            }
        }
    }
}
