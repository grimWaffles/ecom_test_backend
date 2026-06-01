using API_Gateway.Database;
using API_Gateway.Models;
using Microsoft.EntityFrameworkCore;

namespace API_Gateway.Repository
{
    public interface IRequestLogRepository
    {
        Task<IEnumerable<RequestLog>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken);

        Task<RequestLog> CreateAsync(
            RequestLog requestLog,
            CancellationToken cancellationToken);
    }

    public class RequestLogRepository : IRequestLogRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RequestLogRepository> _logger;

        public RequestLogRepository(
            AppDbContext context,
            ILogger<RequestLogRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<RequestLog>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Fetching request logs from {FromUtc} to {ToUtc}",
                fromUtc,
                toUtc);

            try
            {
                var logs = await _context.RequestLogs
                    .AsNoTracking()
                    .Where(r => r.RequestTimeUtc >= fromUtc
                             && r.RequestTimeUtc <= toUtc)
                    .OrderByDescending(r => r.RequestTimeUtc)
                    .ToListAsync(cancellationToken);

                _logger.LogInformation(
                    "Retrieved {Count} request logs between {FromUtc} and {ToUtc}",
                    logs.Count,
                    fromUtc,
                    toUtc);

                return logs;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "GetByDateRangeAsync was cancelled for range {FromUtc} to {ToUtc}",
                    fromUtc,
                    toUtc);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error fetching request logs between {FromUtc} and {ToUtc}",
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
                "Creating request log for path {Path} with trace {TraceIdentifier}",
                requestLog.Path,
                requestLog.TraceIdentifier);

            try
            {
                await _context.RequestLogs.AddAsync(requestLog, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully created request log for trace {TraceIdentifier}",
                    requestLog.TraceIdentifier);

                return requestLog;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "CreateAsync was cancelled for trace {TraceIdentifier}",
                    requestLog.TraceIdentifier);

                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Database error creating request log for trace {TraceIdentifier}",
                    requestLog.TraceIdentifier);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error creating request log for trace {TraceIdentifier}",
                    requestLog.TraceIdentifier);

                throw;
            }
        }
    }
}
