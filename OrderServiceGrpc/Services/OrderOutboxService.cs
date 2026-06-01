using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Repository;

namespace OrderServiceGrpc.Services
{
    public interface IOrderOutboxService
    {
        Task<IEnumerable<OrderOutbox>> GetRecentRecordsToPublishAfterDateAsync(DateTime date);
        Task<(IEnumerable<OrderOutbox> Items, int TotalCount)> GetAllRecordsAsync(int pageNumber, int pageSize, int? statusId = null, string? aggregateType = null);
        Task<OrderOutbox?> GetByIdAsync(int id);
        Task<OrderOutbox> CreateAsync(OrderOutbox entity);
        Task<OrderOutbox> UpdateAsync(OrderOutbox entity);
        Task<bool> DeleteByIdAsync(int id);
        Task<int> DeleteByDateRangeAsync(DateTime from, DateTime to);
    }

    public class OrderOutboxService : IOrderOutboxService
    {
        private readonly IOrderOutboxRepository _repository;
        private readonly ILogger<OrderOutboxService> _logger;

        public OrderOutboxService(IOrderOutboxRepository repository, ILogger<OrderOutboxService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        private void ValidateOrderOutboxEntity(OrderOutbox entity)
        {
            ArgumentNullException.ThrowIfNull(entity, nameof(entity));

            if (entity.AggregateId <= 0)
                throw new ArgumentException("AggregateId must be a positive integer.", nameof(entity.AggregateId));

            if (string.IsNullOrWhiteSpace(entity.AggregateType))
                throw new ArgumentException("AggregateType is required.", nameof(entity.AggregateType));

            if (string.IsNullOrWhiteSpace(entity.EventType))
                throw new ArgumentException("EventType is required.", nameof(entity.EventType));

            if (string.IsNullOrWhiteSpace(entity.Topic))
                throw new ArgumentException("Topic is required.", nameof(entity.Topic));

            if (string.IsNullOrWhiteSpace(entity.Payload))
                throw new ArgumentException("Payload is required.", nameof(entity.Payload));

            if (string.IsNullOrWhiteSpace(entity.PartitionKey))
                throw new ArgumentException("PartitionKey is required.", nameof(entity.PartitionKey));
        }

        public async Task<IEnumerable<OrderOutbox>> GetRecentRecordsToPublishAfterDateAsync(DateTime date)
        {
            _logger.LogInformation("Service — fetching publishable outbox records after {Date}", date);
            try
            {
                if (date > DateTime.UtcNow)
                {
                    _logger.LogWarning("GetRecentRecordsToPublishAfterDate called with a future date: {Date}", date);
                    return Enumerable.Empty<OrderOutbox>();
                }

                IEnumerable<OrderOutbox> records = await _repository.GetRecentRecordsToPublishAfterDateAsync(date);
                _logger.LogInformation("Service — retrieved {Count} publishable records after {Date}", records.Count(), date);
                return records;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — error fetching publishable outbox records after {Date}", date);
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
                "Service — fetching paged outbox records — Page: {Page}, Size: {Size}, StatusId: {StatusId}, AggregateType: {AggregateType}",
                pageNumber, pageSize, statusId, aggregateType);
            try
            {
                if (pageNumber < 1)
                {
                    _logger.LogWarning("Invalid pageNumber {PageNumber} — defaulting to 1", pageNumber);
                    pageNumber = 1;
                }

                (IEnumerable<OrderOutbox> Items, int TotalCount) result = await _repository.GetAllRecordsAsync(pageNumber, pageSize, statusId, aggregateType);
                _logger.LogInformation("Service — retrieved {Count} of {Total} outbox records (page {Page})", result.Items.Count(), result.TotalCount, pageNumber);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — error fetching paged outbox records — Page: {Page}, Size: {Size}", pageNumber, pageSize);
                throw;
            }
        }

        public async Task<OrderOutbox?> GetByIdAsync(int id)
        {
            _logger.LogInformation("Service — fetching OrderOutbox with Id: {Id}", id);
            try
            {
                if (id <= 0)
                {
                    _logger.LogWarning("GetByIdAsync called with invalid Id: {Id}", id);
                    return null;
                }

                OrderOutbox? record = await _repository.GetByIdAsync(id);

                if (record is null)
                    _logger.LogWarning("Service — OrderOutbox with Id: {Id} not found", id);

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — error fetching OrderOutbox with Id: {Id}", id);
                throw;
            }
        }

        public async Task<OrderOutbox> CreateAsync(OrderOutbox entity)
        {
            _logger.LogInformation(
                "Service — creating OrderOutbox — AggregateId: {AggregateId}, EventType: {EventType}, Topic: {Topic}",
                entity?.AggregateId, entity?.EventType, entity?.Topic);
            
            // Validate before entering try-catch for performance
            try
            {
                ValidateOrderOutboxEntity(entity);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Service — validation failed creating OrderOutbox");
                throw;
            }

            try
            {
                OrderOutbox created = await _repository.CreateAsync(entity);
                _logger.LogInformation("Service — created OrderOutbox with Id: {Id}", created.Id);
                return created;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — unexpected error creating OrderOutbox");
                throw;
            }
        }

        public async Task<OrderOutbox> UpdateAsync(OrderOutbox entity)
        {
            _logger.LogInformation("Service — updating OrderOutbox with Id: {Id}", entity?.Id);
            
            // Validate before entering try-catch for performance
            try
            {
                if (entity?.Id <= 0)
                    throw new ArgumentException("Id must be a positive integer.", nameof(entity.Id));

                ValidateOrderOutboxEntity(entity);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Service — validation failed updating OrderOutbox with Id: {Id}", entity?.Id);
                throw;
            }

            try
            {
                OrderOutbox? existing = await _repository.GetByIdAsync(entity.Id);
                if (existing is null)
                    throw new KeyNotFoundException($"OrderOutbox with Id {entity.Id} was not found.");

                OrderOutbox updated = await _repository.UpdateAsync(entity);
                _logger.LogInformation("Service — updated OrderOutbox with Id: {Id}", updated.Id);
                return updated;
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Service — OrderOutbox with Id: {Id} not found for update", entity.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — unexpected error updating OrderOutbox with Id: {Id}", entity.Id);
                throw;
            }
        }

        public async Task<bool> DeleteByIdAsync(int id)
        {
            _logger.LogInformation("Service — deleting OrderOutbox with Id: {Id}", id);
            try
            {
                if (id <= 0)
                    throw new ArgumentException("Id must be a positive integer.", nameof(id));

                bool deleted = await _repository.DeleteByIdAsync(id);

                if (!deleted)
                    _logger.LogWarning("Service — OrderOutbox with Id: {Id} not found for deletion", id);
                else
                    _logger.LogInformation("Service — deleted OrderOutbox with Id: {Id}", id);

                return deleted;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Service — validation failed deleting OrderOutbox with Id: {Id}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — unexpected error deleting OrderOutbox with Id: {Id}", id);
                throw;
            }
        }

        public async Task<int> DeleteByDateRangeAsync(DateTime from, DateTime to)
        {
            _logger.LogInformation("Service — deleting OrderOutbox records between {From} and {To}", from, to);
            try
            {
                if (from > to)
                    throw new ArgumentException($"'from' date ({from}) must not be later than 'to' date ({to}).");

                if (to > DateTime.UtcNow)
                    _logger.LogWarning("DeleteByDateRange — 'to' date {To} is in the future, this may delete unprocessed records", to);

                int deleted = await _repository.DeleteByDateRangeAsync(from, to);
                _logger.LogInformation("Service — deleted {Count} OrderOutbox records between {From} and {To}", deleted, from, to);
                return deleted;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Service — invalid date range [{From} - {To}]", from, to);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — unexpected error deleting OrderOutbox records between {From} and {To}", from, to);
                throw;
            }
        }
    }
}
