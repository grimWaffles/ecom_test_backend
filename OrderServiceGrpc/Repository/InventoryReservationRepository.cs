using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Repository
{
    public interface IInventoryReservationRepository
    {
        Task<int> GetActiveLockedQuantityByProductIdAsync(int productId, long? excludeReservationId = null);
        Task<InventoryReservation?> GetByCartIdAsync(long cartId);
        Task<InventoryReservation> CreateAsync(InventoryReservation entity, int userId);
        Task<InventoryReservation?> UpdateAsync(InventoryReservation entity, int userId);
        Task<bool> DeleteAsync(long id, int userId);
    }

    public class InventoryReservationRepository : IInventoryReservationRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<InventoryReservationRepository> _logger;
        private readonly UnitOfWorkContext _uowContext;

        public InventoryReservationRepository(
            AppDbContext context,
            ILogger<InventoryReservationRepository> logger,
            UnitOfWorkContext uowContext)
        {
            _context = context;
            _logger = logger;
            _uowContext = uowContext;
        }

        public async Task<int> GetActiveLockedQuantityByProductIdAsync(int productId, long? excludeReservationId = null)
        {
            _logger.LogInformation("Calculating locked quantity for ProductId: {ProductId}", productId);

            try
            {
                var mainQuery = _context.Inventory
                    .AsNoTracking()
                    .Where(iv => iv.ProductId == productId)
                    .Select(iv => iv.Quantity - (
                        _context.InventoryReservations
                            .Where(x => !x.IsDeleted
                                && x.ProductId == productId
                                && x.LockExpirationDate > DateTime.UtcNow
                            )
                            .Sum(iv => (int?)iv.LockQuantity ?? 0)
                    ));

                int lockedQuantity = await mainQuery.SingleOrDefaultAsync();

                _logger.LogInformation("Locked quantity for ProductId: {ProductId} is {LockedQuantity}", productId, lockedQuantity);
                return lockedQuantity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating locked quantity for ProductId: {ProductId}", productId);
                throw;
            }
        }

        public async Task<InventoryReservation?> GetByCartIdAsync(long cartId)
        {
            _logger.LogInformation("Fetching InventoryReservation for CartId: {CartId}", cartId);
            try
            {
                InventoryReservation? record = await _context.InventoryReservations
                    .FirstOrDefaultAsync(x => x.CartId == cartId && !x.IsDeleted);

                if (record is null)
                    _logger.LogWarning("No InventoryReservation found for CartId: {CartId}", cartId);

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching InventoryReservation for CartId: {CartId}", cartId);
                throw;
            }
        }

        public async Task<InventoryReservation> CreateAsync(InventoryReservation entity, int userId)
        {
            _logger.LogInformation("Creating InventoryReservation — ProductId: {ProductId}, CartId: {CartId}, LockQuantity: {LockQuantity}",
                entity.ProductId, entity.CartId, entity.LockQuantity);
            try
            {
                entity.CreatedBy = userId;
                entity.CreatedDate = DateTime.UtcNow;
                entity.IsDeleted = false;

                await _context.InventoryReservations.AddAsync(entity);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Created InventoryReservation with Id: {Id}", entity.Id);
                return entity;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating InventoryReservation — ProductId: {ProductId}", entity.ProductId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating InventoryReservation");
                throw;
            }
        }

        public async Task<InventoryReservation?> UpdateAsync(InventoryReservation entity, int userId)
        {
            _logger.LogInformation("Updating InventoryReservation with Id: {Id}", entity.Id);
            try
            {
                InventoryReservation? existing = await _context.InventoryReservations
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDeleted);

                if (existing is null)
                {
                    _logger.LogWarning("InventoryReservation with Id: {Id} not found for update", entity.Id);
                    return null;
                }

                existing.LockQuantity = entity.LockQuantity;
                existing.LockExpirationDate = entity.LockExpirationDate;

                if (!_uowContext.IsUnderUnitOfWork)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Updated InventoryReservation with Id: {Id}", existing.Id);
                return existing;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating InventoryReservation with Id: {Id}", entity.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating InventoryReservation with Id: {Id}", entity.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(long id, int userId)
        {
            _logger.LogInformation("Deleting InventoryReservation with Id: {Id}", id);
            try
            {
                InventoryReservation? existing = await _context.InventoryReservations
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (existing is null)
                {
                    _logger.LogWarning("InventoryReservation with Id: {Id} not found for delete", id);
                    return false;
                }

                existing.IsDeleted = true;

                if (!_uowContext.IsUnderUnitOfWork)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Deleted InventoryReservation with Id: {Id}", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting InventoryReservation with Id: {Id}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting InventoryReservation with Id: {Id}", id);
                throw;
            }
        }
    }
}
