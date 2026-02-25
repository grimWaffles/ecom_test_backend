using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Repository;
using System.Transactions;

namespace OrderServiceGrpc.Services
{
    public interface ICustomerTransactionProcessorService
    {
        Task<CustomerTransactionDto> GetTransactionById(int id);
        Task<List<CustomerTransactionDto>> GetAllTransactions();
        Task<bool> AddTransaction(CustomerTransactionDto request, int userId);
        Task<bool> UpdateTransaction(CustomerTransactionDto request, int userId);
        Task<bool> DeleteTransaction(CustomerTransactionDto request, int userId);
        Task<int> GetTransactionCount();
        Task<PagedTransactionResultService> GetAllTransactionsWithPagination(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string transactionType);
    }

    public class CustomerTransactionProcessorService : ICustomerTransactionProcessorService
    {
        private readonly ICustomerTransactionRepository _repo;
        public CustomerTransactionProcessorService(ICustomerTransactionRepository repo)
        {
            _repo = repo;
        }

        public async Task<bool> AddTransaction(CustomerTransactionDto request, int userId)
        {
            try
            {
                CustomerTransactionModel model = TransactionMapper.DtoToEntity(request);

                return await _repo.AddTransaction(model, userId);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> DeleteTransaction(CustomerTransactionDto request, int userId)
        {
            try
            {
                CustomerTransactionModel model = TransactionMapper.DtoToEntity(request);

                return await _repo.DeleteTransaction(model, userId);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<CustomerTransactionDto>> GetAllTransactions()
        {
            try
            {
                List<CustomerTransactionModel> list =  await _repo.GetAllTransactions();

                if(list == null)
                {
                    return null;
                }

                return list.Select(x => TransactionMapper.EntityToDto(x)).ToList(); 
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<PagedTransactionResultService> GetAllTransactionsWithPagination(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string transactionType)
        {
            try
            {
                PagedTransactionResultRepo repoResult = await _repo.GetAllTransactionsWithPagination(startDate, endDate, pageNumber, pageSize, transactionType);

                PagedTransactionResultService result = new PagedTransactionResultService()
                {
                    ListOfTransactions = repoResult.ListOfTransactions.Select(x => TransactionMapper.EntityToDto(x)).ToList(),
                    TotalPages = repoResult.TotalPages,
                    TotalTransactions = repoResult.TotalTransactions,
                    Status = repoResult.Status,
                    ErrorMessage = repoResult.ErrorMessage
                };

                return result;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<CustomerTransactionDto> GetTransactionById(int id)
        {
            try
            {
                CustomerTransactionModel model = await _repo.GetTransactionById(id);

                return TransactionMapper.EntityToDto(model);    
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<int> GetTransactionCount()
        {
            try
            {
                return await _repo.GetTransactionCount();   
            }
            catch (Exception ex)
            {
                return -1;
            }
        }

        public async Task<bool> UpdateTransaction(CustomerTransactionDto request, int userId)
        {
            try
            {
                return await _repo.UpdateTransaction(TransactionMapper.DtoToEntity(request), userId);

            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
