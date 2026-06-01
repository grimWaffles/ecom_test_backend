using Microsoft.EntityFrameworkCore;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Repository
{
    public interface IOutboxStatusRepository
    {
        Task<IEnumerable<OutboxStatus>> GetAllOutboxStatusAsync();
        Task<OutboxStatus?> GetOutboxStatusByIdAsync(int statusId);
        Task<OutboxStatus> CreateAsync(OutboxStatus entity);
        Task<OutboxStatus> UpdateAsync(OutboxStatus entity);
        Task<bool> DeleteAsync(int statusId);
    }
    public class OutboxStatusRepository : IOutboxStatusRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OutboxStatusRepository> _logger;

        public OutboxStatusRepository(AppDbContext context, ILogger<OutboxStatusRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<OutboxStatus>> GetAllOutboxStatusAsync()
        {
            _logger.LogInformation("Fetching all OutboxStatus records");
            try
            {
                List<OutboxStatus> statuses = await _context.OutboxStatus
                    .AsNoTracking()
                    .OrderBy(s => s.StatusId)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} OutboxStatus records", statuses.Count);
                return statuses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all OutboxStatus records");
                throw;
            }
        }

        public async Task<OutboxStatus?> GetOutboxStatusByIdAsync(int statusId)
        {
            _logger.LogInformation("Fetching OutboxStatus with StatusId: {StatusId}", statusId);
            try
            {
                OutboxStatus? status = await _context.OutboxStatus
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.StatusId == statusId);

                if (status is null)
                    _logger.LogWarning("OutboxStatus with StatusId: {StatusId} not found", statusId);
                else
                    _logger.LogInformation("Retrieved OutboxStatus: {StatusName}", status.StatusName);

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching OutboxStatus with StatusId: {StatusId}", statusId);
                throw;
            }
        }

        public async Task<OutboxStatus> CreateAsync(OutboxStatus entity)
        {
            _logger.LogInformation("Creating OutboxStatus — StatusId: {StatusId}, StatusName: {StatusName}",
                entity.StatusId, entity.StatusName);
            try
            {
                await _context.OutboxStatus.AddAsync(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created OutboxStatus with StatusId: {StatusId}", entity.StatusId);
                return entity;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating OutboxStatus — StatusId: {StatusId}", entity.StatusId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating OutboxStatus");
                throw;
            }
        }

        public async Task<OutboxStatus> UpdateAsync(OutboxStatus entity)
        {
            _logger.LogInformation("Updating OutboxStatus with StatusId: {StatusId}", entity.StatusId);
            try
            {
                _context.OutboxStatus.Update(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated OutboxStatus with StatusId: {StatusId}", entity.StatusId);
                return entity;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict updating OutboxStatus with StatusId: {StatusId}", entity.StatusId);
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating OutboxStatus with StatusId: {StatusId}", entity.StatusId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating OutboxStatus with StatusId: {StatusId}", entity.StatusId);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int statusId)
        {
            _logger.LogInformation("Deleting OutboxStatus with StatusId: {StatusId}", statusId);
            try
            {
                OutboxStatus? status = await _context.OutboxStatus.FindAsync(statusId);
                if (status is null)
                {
                    _logger.LogWarning("DeleteAsync — OutboxStatus with StatusId: {StatusId} not found", statusId);
                    return false;
                }

                _context.OutboxStatus.Remove(status);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted OutboxStatus with StatusId: {StatusId}", statusId);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting OutboxStatus with StatusId: {StatusId}", statusId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting OutboxStatus with StatusId: {StatusId}", statusId);
                throw;
            }
        }
    }
}
