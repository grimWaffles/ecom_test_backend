using Microsoft.EntityFrameworkCore;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Repository
{
    public interface IOrderOutboxRepository
    {
        Task<IEnumerable<OrderOutbox>> GetRecentRecordsToPublishAfterDateAsync(DateTime date);
        Task<(IEnumerable<OrderOutbox> Items, int TotalCount)> GetAllRecordsAsync(int pageNumber, int pageSize, int? statusId = null, string? aggregateType = null);
        Task<OrderOutbox?> GetByIdAsync(int id);
        Task<OrderOutbox> CreateAsync(OrderOutbox entity);
        Task<OrderOutbox> UpdateAsync(OrderOutbox entity);
        Task<bool> DeleteByIdAsync(int id);
        Task<int> DeleteByDateRangeAsync(DateTime from, DateTime to);
    }

    public class OrderOutboxRepository : IOrderOutboxRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrderOutboxRepository> _logger;
        private readonly UnitOfWorkContext _uowContext;

        public OrderOutboxRepository(AppDbContext context, ILogger<OrderOutboxRepository> logger, UnitOfWorkContext unitOfWorkContext)
        {
            _context = context;
            _logger = logger;
            _uowContext = unitOfWorkContext;
        }

        public async Task<IEnumerable<OrderOutbox>> GetRecentRecordsToPublishAfterDateAsync(DateTime date)
        {
            _logger.LogInformation("Fetching outbox records with ScheduledAt >= {Date} and StatusId IN (1, 4)", date);
            try
            {
                List<OrderOutbox> records = await _context.OrderOutbox
                    .Include(o => o.Status)
                    .Where(o => o.ScheduledAt >= date && (o.StatusId == 1 || o.StatusId == 4))
                    .OrderBy(o => o.ScheduledAt)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} publishable outbox records after {Date}", records.Count, date);
                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching publishable outbox records after {Date}", date);
                throw;
            }
        }

        public async Task<(IEnumerable<OrderOutbox> Items, int TotalCount)> GetAllRecordsAsync(
            int pageNumber,
            int pageSize,
            int? statusId = null,
            string? aggregateType = null)
        {
            _logger.LogInformation(
                "Fetching paged outbox records — Page: {Page}, Size: {Size}, StatusId: {StatusId}, AggregateType: {AggregateType}",
                pageNumber, pageSize, statusId, aggregateType);
            try
            {
                IQueryable<OrderOutbox> query = _context.OrderOutbox
                    .Include(o => o.Status)
                    .AsNoTracking()
                    .AsQueryable();

                if (statusId.HasValue)
                    query = query.Where(o => o.StatusId == statusId.Value);

                if (!string.IsNullOrWhiteSpace(aggregateType))
                    query = query.Where(o => o.AggregateType == aggregateType);

                int totalCount = await query.CountAsync();

                List<OrderOutbox> items = await query
                    .OrderByDescending(o => o.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} of {Total} outbox records (page {Page})", items.Count, totalCount, pageNumber);
                return (items, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error fetching paged outbox records — Page: {Page}, Size: {Size}", pageNumber, pageSize);
                throw;
            }
        }

        public async Task<OrderOutbox?> GetByIdAsync(int id)
        {
            _logger.LogInformation("Fetching OrderOutbox with Id: {Id}", id);
            try
            {
                OrderOutbox? record = await _context.OrderOutbox
                    .Include(o => o.Status)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (record is null)
                    _logger.LogWarning("OrderOutbox with Id: {Id} not found", id);
                else
                    _logger.LogInformation("Retrieved OrderOutbox with Id: {Id}", id);

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching OrderOutbox with Id: {Id}", id);
                throw;
            }
        }

        public async Task<OrderOutbox> CreateAsync(OrderOutbox entity)
        {
            _logger.LogInformation(
                "Creating OrderOutbox — AggregateId: {AggregateId}, EventType: {EventType}, Topic: {Topic}",
                entity.AggregateId, entity.EventType, entity.Topic);
            try
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.ScheduledAt = DateTime.UtcNow;
                entity.StatusId = 1;
                entity.RetryCount = 0;

                await _context.OrderOutbox.AddAsync(entity);

                if (!_uowContext.IsUnderUnitOfWork)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Created OrderOutbox with Id: {Id}", entity.Id);
                return entity;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex,
                    "Database error creating OrderOutbox — AggregateId: {AggregateId}, EventType: {EventType}",
                    entity.AggregateId, entity.EventType);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating OrderOutbox");
                throw;
            }
        }

        public async Task<OrderOutbox> UpdateAsync(OrderOutbox entity)
        {
            _logger.LogInformation("Updating OrderOutbox with Id: {Id}, StatusId: {StatusId}", entity.Id, entity.StatusId);
            try
            {
                _context.OrderOutbox.Update(entity);
                if (!_uowContext.IsUnderUnitOfWork)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Updated OrderOutbox with Id: {Id}", entity.Id);
                return entity;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict updating OrderOutbox with Id: {Id}", entity.Id);
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating OrderOutbox with Id: {Id}", entity.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating OrderOutbox with Id: {Id}", entity.Id);
                throw;
            }
        }

        public async Task<bool> DeleteByIdAsync(int id)
        {
            _logger.LogInformation("Deleting OrderOutbox with Id: {Id}", id);
            try
            {
                OrderOutbox? record = await _context.OrderOutbox.FindAsync(id);
                if (record is null)
                {
                    _logger.LogWarning("DeleteById — OrderOutbox with Id: {Id} not found", id);
                    return false;
                }

                _context.OrderOutbox.Remove(record);
                if (!_uowContext.IsUnderUnitOfWork)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Deleted OrderOutbox with Id: {Id}", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting OrderOutbox with Id: {Id}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting OrderOutbox with Id: {Id}", id);
                throw;
            }
        }

        public async Task<int> DeleteByDateRangeAsync(DateTime from, DateTime to)
        {
            _logger.LogInformation("Deleting OrderOutbox records with CreatedAt between {From} and {To}", from, to);
            try
            {
                int deleted = await _context.OrderOutbox
                    .Where(o => o.CreatedAt >= from && o.CreatedAt <= to)
                    .ExecuteDeleteAsync();

                _logger.LogInformation("Deleted {Count} OrderOutbox records between {From} and {To}", deleted, from, to);
                return deleted;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting OrderOutbox records between {From} and {To}", from, to);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting OrderOutbox records between {From} and {To}", from, to);
                throw;
            }
        }
    }
}
