using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Repository;
using System.Data;

namespace OrderServiceGrpc.Helpers
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        IOrderRepository Orders { get; }
        IOrderOutboxRepository Outbox { get; }
        IDbConnection Connection { get; }
        IDbTransaction DbTransaction { get; }
        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitAsync();
        Task RollbackAsync();
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private readonly UnitOfWorkContext _unitOfWorkContext;
        private IDbContextTransaction? _transaction;
        private readonly ILogger<UnitOfWork> _logger;

        public IOrderRepository Orders { get; }
        public IOrderOutboxRepository Outbox { get; }
        public IDbConnection Connection => _context.Database.GetDbConnection();
        public IDbTransaction? DbTransaction => _transaction?.GetDbTransaction();

        public UnitOfWork(AppDbContext appDbContext, UnitOfWorkContext unitOfWorkContext, IOrderRepository orderRepository, IOrderOutboxRepository orderOutboxRepository, ILogger<UnitOfWork> logger)
        {
            _context = appDbContext;
            _unitOfWorkContext = unitOfWorkContext;
            Orders = orderRepository;
            Outbox  = orderOutboxRepository;
            _logger = logger;
        }

        public async Task BeginTransactionAsync()
        {
            _logger.LogInformation("UnitOfWork — beginning transaction");
            
            //if(_context.Database.GetDbConnection().State != ConnectionState.Open)
                //await _context.Database.GetDbConnection().OpenAsync();
            
            _transaction = await _context.Database.BeginTransactionAsync();
            
            _unitOfWorkContext.Begin();
        }

        public async Task<int> SaveChangesAsync()
        {
            _logger.LogInformation("UnitOfWork — saving changes");
            return await _context.SaveChangesAsync();
        }

        public async Task CommitAsync()
        {
            _logger.LogInformation("UnitOfWork — committing transaction");
            try
            {
                if (_transaction is not null)
                {
                    await _transaction.CommitAsync();
                    _logger.LogInformation("UnitOfWork — transaction committed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UnitOfWork — commit failed, rolling back");
                await RollbackAsync();
                throw;
            }
            finally
            {
                await DisposeTransactionAsync();
            }
        }

        public async Task RollbackAsync()
        {
            _logger.LogWarning("UnitOfWork — rolling back transaction");
            try
            {
                if (_transaction is not null)
                    await _transaction.RollbackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UnitOfWork — rollback failed");
                throw;
            }
            finally
            {
                await DisposeTransactionAsync();
            }
        }

        private async Task DisposeTransactionAsync()
        {
            if (_transaction is not null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
            _unitOfWorkContext.End();
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeTransactionAsync();
            await _context.DisposeAsync();
        }
    }
}
