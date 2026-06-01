using API_Gateway.Models;
using API_Gateway.Repository;

namespace API_Gateway.Services
{
    public interface IRequestLogService
    {
        Task<IEnumerable<RequestLog>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken);

        Task<RequestLog> CreateAsync(
            RequestLog requestLog,
            CancellationToken cancellationToken);
    }

    public class RequestLogService : IRequestLogService
    {
        private readonly IRequestLogRepository _repository;
        private readonly ILogger<RequestLogService> _logger;

        public RequestLogService(
            IRequestLogRepository repository,
            ILogger<RequestLogService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<IEnumerable<RequestLog>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Service: GetByDateRangeAsync called with range {FromUtc} to {ToUtc}",
                fromUtc,
                toUtc);

            try
            {
                if (fromUtc > toUtc)
                {
                    _logger.LogWarning(
                        "Invalid date range: {FromUtc} is after {ToUtc}",
                        fromUtc,
                        toUtc);

                    throw new ArgumentException(
                        $"fromUtc ({fromUtc}) must be before or equal to toUtc ({toUtc}).");
                }

                var logs = await _repository.GetByDateRangeAsync(
                    fromUtc,
                    toUtc,
                    cancellationToken);

                return logs;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "GetByDateRangeAsync was cancelled at service layer");

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Service error in GetByDateRangeAsync for range {FromUtc} to {ToUtc}",
                    fromUtc,
                    toUtc);

                throw;
            }
        }

        public async Task<RequestLog> CreateAsync(
            RequestLog requestLog,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Service: CreateAsync called for trace {TraceIdentifier}",
                requestLog.TraceIdentifier);

            try
            {
                var created = await _repository.CreateAsync(
                    requestLog,
                    cancellationToken);

                return created;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "CreateAsync was cancelled at service layer " +
                    "for trace {TraceIdentifier}",
                    requestLog.TraceIdentifier);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Service error in CreateAsync for trace {TraceIdentifier}",
                    requestLog.TraceIdentifier);

                throw;
            }
        }
    }
}
