using Microsoft.EntityFrameworkCore;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Repository
{
    public interface IInventoryRepository
    {
        Task<List<Inventory>> GetAllAsync(int pageNumber, int pageSize, bool track = false);
        Task<List<Inventory>> GetByProductIdAsync(int productId, bool track = false);
        Task<Inventory> CreateAsync(Inventory entity, int userId);
        Task<Inventory?> UpdateAsync(Inventory entity, int userId);
        Task<bool> DeleteAsync(int id, int userId);
        Task<List<Inventory>> GetByProductCategoryAsync(int productCategoryId, int pageNumber, int pageSize, bool track = false);
    }

    public class InventoryRepository : IInventoryRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InventoryRepository> _logger;
        private readonly UnitOfWorkContext _uowContext;

        public InventoryRepository(
            AppDbContext context,
            ILogger<InventoryRepository> logger,
            UnitOfWorkContext uowContext)
        {
            _context = context;
            _logger = logger;
            _uowContext = uowContext;
        }

        public async Task<List<Inventory>> GetAllAsync(int pageNumber, int pageSize, bool track = false)
        {
            _logger.LogInformation("Fetching Inventory — PageNumber: {PageNumber}, PageSize: {PageSize}", pageNumber, pageSize);
            try
            {
                IQueryable<Inventory> query = _context.Inventory
                    .WithTracking(track: track)
                    .Where(x => !x.IsDeleted)
                    .OrderBy(x => x.Id)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize);

                List<Inventory> records = await query.ToListAsync();

                _logger.LogInformation("Retrieved {Count} Inventory records", records.Count);
                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Inventory — PageNumber: {PageNumber}, PageSize: {PageSize}", pageNumber, pageSize);
                throw;
            }
        }

        public async Task<List<Inventory>> GetByProductIdAsync(int productId, bool track = false)
        {
            _logger.LogInformation("Fetching Inventory with ProductId: {ProductId}", productId);
            try
            {
                IQueryable<Inventory> query = _context.Inventory
                    .WithTracking(track: track)
                    .Where(x => !x.IsDeleted && x.ProductId == productId);

                List<Inventory> records = await query.ToListAsync();

                if (records.Count == 0)
                    _logger.LogWarning("No Inventory found with ProductId: {ProductId}", productId);
                else
                    _logger.LogInformation("Retrieved {Count} Inventory records with ProductId: {ProductId}", records.Count, productId);

                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Inventory with ProductId: {ProductId}", productId);
                throw;
            }
        }

        public async Task<Inventory> CreateAsync(Inventory entity, int userId)
        {
            _logger.LogInformation("Creating Inventory — ProductId: {ProductId}, Quantity: {Quantity}", entity.ProductId, entity.Quantity);
            try
            {
                entity.CreatedBy = userId;
                entity.CreatedDate = DateTime.UtcNow;
                entity.IsDeleted = false;

                await _context.Inventory.AddAsync(entity);

                if (!_uowContext.IsUnderUnitOfWork)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Created Inventory with Id: {Id}", entity.Id);
                return entity;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating Inventory — ProductId: {ProductId}", entity.ProductId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating Inventory");
                throw;
            }
        }

        public async Task<Inventory?> UpdateAsync(Inventory entity, int userId)
        {
            _logger.LogInformation("Updating Inventory with Id: {Id}", entity.Id);
            try
            {
                Inventory? existing = await _context.Inventory
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDeleted);

                if (existing is null)
                {
                    _logger.LogWarning("Inventory with Id: {Id} not found for update", entity.Id);
                    return null;
                }

                existing.ProductId = entity.ProductId;
                existing.Quantity = entity.Quantity;
                existing.ModifiedBy = userId;
                existing.ModifiedDate = DateTime.UtcNow;

                if (!_uowContext.IsUnderUnitOfWork)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Updated Inventory with Id: {Id}", existing.Id);
                return existing;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating Inventory with Id: {Id}", entity.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating Inventory with Id: {Id}", entity.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id, int userId)
        {
            _logger.LogInformation("Deleting Inventory with Id: {Id}", id);
            try
            {
                Inventory? existing = await _context.Inventory
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (existing is null)
                {
                    _logger.LogWarning("Inventory with Id: {Id} not found for delete", id);
                    return false;
                }

                existing.IsDeleted = true;
                existing.ModifiedBy = userId;
                existing.ModifiedDate = DateTime.UtcNow;

                if (!_uowContext.IsUnderUnitOfWork)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Deleted Inventory with Id: {Id}", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting Inventory with Id: {Id}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting Inventory with Id: {Id}", id);
                throw;
            }
        }

        public async Task<List<Inventory>> GetByProductCategoryAsync(int productCategoryId, int pageNumber, int pageSize, bool track = false)
        {
            _logger.LogInformation(
                "Fetching Inventory — ProductCategoryId: {ProductCategoryId}, PageNumber: {PageNumber}, PageSize: {PageSize}",
                productCategoryId, pageNumber, pageSize);
            try
            {
                IQueryable<Inventory> query = _context.Inventory
                    .WithTracking(track: track)
                    .Where(x => !x.IsDeleted && x.ProductCategoryId == productCategoryId);

                query = query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize);

                List<Inventory> records = await query.ToListAsync();

                _logger.LogInformation("Retrieved {Count} Inventory records for ProductCategoryId: {ProductCategoryId}", records.Count, productCategoryId);
                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Inventory for ProductCategoryId: {ProductCategoryId}", productCategoryId);
                throw;
            }
        }
    }
}
