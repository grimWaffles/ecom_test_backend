using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Repository;

namespace OrderServiceGrpc.Services
{
    public interface IOutboxStatusService
    {
        Task<IEnumerable<OutboxStatus>> GetAllOutboxStatusAsync();
        Task<OutboxStatus?> GetOutboxStatusByIdAsync(int statusId);
        Task<OutboxStatus> CreateAsync(OutboxStatus entity);
        Task<OutboxStatus> UpdateAsync(OutboxStatus entity);
        Task<bool> DeleteAsync(int statusId);
    }
    public class OutboxStatusService : IOutboxStatusService
    {
        private readonly IOutboxStatusRepository _repository;
        private readonly ILogger<OutboxStatusService> _logger;

        public OutboxStatusService(IOutboxStatusRepository repository, ILogger<OutboxStatusService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<IEnumerable<OutboxStatus>> GetAllOutboxStatusAsync()
        {
            _logger.LogInformation("Service — fetching all OutboxStatus records");
            try
            {
                IEnumerable<OutboxStatus> statuses = await _repository.GetAllOutboxStatusAsync();
                _logger.LogInformation("Service — retrieved {Count} OutboxStatus records", statuses.Count());
                return statuses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — error fetching all OutboxStatus records");
                throw;
            }
        }

        public async Task<OutboxStatus?> GetOutboxStatusByIdAsync(int statusId)
        {
            _logger.LogInformation("Service — fetching OutboxStatus with StatusId: {StatusId}", statusId);
            try
            {
                if (statusId <= 0)
                {
                    _logger.LogWarning("GetOutboxStatusByIdAsync called with invalid StatusId: {StatusId}", statusId);
                    return null;
                }

                OutboxStatus? status = await _repository.GetOutboxStatusByIdAsync(statusId);

                if (status is null)
                    _logger.LogWarning("Service — OutboxStatus with StatusId: {StatusId} not found", statusId);

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — error fetching OutboxStatus with StatusId: {StatusId}", statusId);
                throw;
            }
        }

        public async Task<OutboxStatus> CreateAsync(OutboxStatus entity)
        {
            _logger.LogInformation("Service — creating OutboxStatus — StatusId: {StatusId}, StatusName: {StatusName}",
                entity.StatusId, entity.StatusName);
            try
            {
                if (entity.StatusId <= 0)
                    throw new ArgumentException("StatusId must be a positive integer.", nameof(entity));

                if (string.IsNullOrWhiteSpace(entity.StatusName))
                    throw new ArgumentException("StatusName is required.", nameof(entity));

                OutboxStatus? existing = await _repository.GetOutboxStatusByIdAsync(entity.StatusId);
                if (existing is not null)
                    throw new InvalidOperationException($"OutboxStatus with StatusId {entity.StatusId} already exists.");

                OutboxStatus created = await _repository.CreateAsync(entity);
                _logger.LogInformation("Service — created OutboxStatus with StatusId: {StatusId}", created.StatusId);
                return created;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Service — validation failed creating OutboxStatus");
                throw;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Service — duplicate StatusId {StatusId} on create", entity.StatusId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — unexpected error creating OutboxStatus");
                throw;
            }
        }

        public async Task<OutboxStatus> UpdateAsync(OutboxStatus entity)
        {
            _logger.LogInformation("Service — updating OutboxStatus with StatusId: {StatusId}", entity.StatusId);
            try
            {
                if (entity.StatusId <= 0)
                    throw new ArgumentException("StatusId must be a positive integer.", nameof(entity));

                if (string.IsNullOrWhiteSpace(entity.StatusName))
                    throw new ArgumentException("StatusName is required.", nameof(entity));

                OutboxStatus? existing = await _repository.GetOutboxStatusByIdAsync(entity.StatusId);
                if (existing is null)
                    throw new KeyNotFoundException($"OutboxStatus with StatusId {entity.StatusId} was not found.");

                OutboxStatus updated = await _repository.UpdateAsync(entity);
                _logger.LogInformation("Service — updated OutboxStatus with StatusId: {StatusId}", updated.StatusId);
                return updated;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Service — validation failed updating OutboxStatus with StatusId: {StatusId}", entity.StatusId);
                throw;
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Service — OutboxStatus with StatusId: {StatusId} not found for update", entity.StatusId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — unexpected error updating OutboxStatus with StatusId: {StatusId}", entity.StatusId);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int statusId)
        {
            _logger.LogInformation("Service — deleting OutboxStatus with StatusId: {StatusId}", statusId);
            try
            {
                if (statusId <= 0)
                    throw new ArgumentException("StatusId must be a positive integer.", nameof(statusId));

                int[] seedIds = { 1, 2, 3, 4 };
                if (seedIds.Contains(statusId))
                    throw new InvalidOperationException($"StatusId {statusId} is a system-seeded status and cannot be deleted.");

                bool deleted = await _repository.DeleteAsync(statusId);

                if (!deleted)
                    _logger.LogWarning("Service — OutboxStatus with StatusId: {StatusId} not found for deletion", statusId);
                else
                    _logger.LogInformation("Service — deleted OutboxStatus with StatusId: {StatusId}", statusId);

                return deleted;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Service — validation failed deleting OutboxStatus with StatusId: {StatusId}", statusId);
                throw;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Service — attempted deletion of protected StatusId: {StatusId}", statusId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service — unexpected error deleting OutboxStatus with StatusId: {StatusId}", statusId);
                throw;
            }
        }
    }
}
