using OrderServiceGrpc.Helpers.Converters;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Repository;
using System.Diagnostics;
using System.Text;
using System.Transactions;

namespace OrderServiceGrpc.Services
{
    public interface ICustomerTransactionProcessorService
    {
        Task<CustomerTransactionDto> GetTransactionById(int id);
        Task<List<CustomerTransactionDto>> GetAllTransactions();
        Task<int> AddTransaction(CustomerTransactionDto request, int userId);
        Task<bool> UpdateTransaction(CustomerTransactionDto request, int userId);
        Task<bool> DeleteTransaction(CustomerTransactionDto request, int userId);
        Task<PagedTransactionResultFromService> GetAllTransactionsWithPagination(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string transactionType);
        Task<ConsumerResponseModel> TestCustomerTransactionProcessorService();
    }

    public class CustomerTransactionProcessorService : ICustomerTransactionProcessorService
    {
        private readonly ICustomerTransactionRepository _repo;
        private readonly ILogger<CustomerTransactionProcessorService> _logger;

        public CustomerTransactionProcessorService(ILogger<CustomerTransactionProcessorService> logger,ICustomerTransactionRepository repo)
        {
            _repo = repo;
            _logger = logger;   
        }

        public async Task<int> AddTransaction(CustomerTransactionDto request, int userId)
        {
            int result = -1;
            try
            {
                CustomerTransactionModel model = TransactionMapper.DtoToEntity(request);
                int totalTransactionsToday = await _repo.GetTotalTransactionCountForUser(request.UserId);

                if (totalTransactionsToday < 0)
                {
                    return result;
                }

                model.TransactionKey = GenerateTransactionKey(model.UserId, model.TransactionType, model.TransactionDate, totalTransactionsToday);

                if (await _repo.CheckIfTransactionKeyExists(model.TransactionKey) > 0)
                {
                    return result;
                }

                result = await _repo.AddTransaction(model, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error:{Message}. StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
            }

            return result;
        }

        private string GenerateTransactionKey(int userId, string trxType, DateTime trxDate, int totalTransactionsToday)
        {
            string tCounter = totalTransactionsToday.ToString();
            tCounter.PadLeft(2, '0');

            return $"{userId}-{trxType}-{trxDate.ToString("yyyyMMdd")}-{tCounter}";
        }

        public async Task<bool> DeleteTransaction(CustomerTransactionDto request, int userId)
        {
            bool result = false;
            try
            {
                if (await _repo.CheckIfTransactionKeyExists(request.TransactionKey) <= 0)
                {
                    return false;
                }

                CustomerTransactionModel model = TransactionMapper.DtoToEntity(request);
                //model.TransactionKey = model.TransactionKey.Substring(0, model.TransactionKey.Length - 2) + "01";
                result = await _repo.DeleteTransaction(model, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error:{Message}. StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
            }

            return result;
        }

        public async Task<List<CustomerTransactionDto>> GetAllTransactions()
        {
            try
            {
                List<CustomerTransactionModel> list = await _repo.GetAllTransactions();

                if (list == null)
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

        public async Task<PagedTransactionResultFromService> GetAllTransactionsWithPagination(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string transactionType)
        {
            try
            {
                PagedTransactionResultFromRepo repoResult = await _repo.GetAllTransactionsWithPagination(startDate, endDate, pageNumber, pageSize, transactionType);

                PagedTransactionResultFromService result = new PagedTransactionResultFromService()
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
                return new PagedTransactionResultFromService()
                {
                    ListOfTransactions = null,
                    TotalPages = 0,
                    TotalTransactions = 0,
                    Status = false,
                    ErrorMessage = "Failed to fetch transactions"
                };
            }
        }

        public async Task<CustomerTransactionDto> GetTransactionById(int id)
        {
            try
            {
                CustomerTransactionModel model = await _repo.GetTransactionById(id);

                if (model == null)
                {
                    return null;
                }

                return TransactionMapper.EntityToDto(model);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<bool> UpdateTransaction(CustomerTransactionDto request, int userId)
        {
            try
            {
                if (await _repo.CheckIfTransactionKeyExists(request.TransactionKey) <= 0)
                {
                    return false;
                }

                bool result = await _repo.UpdateTransaction(TransactionMapper.DtoToEntity(request), userId);

                return result;

            }
            catch (Exception ex)
            {
                return false;
            }
        }

        //Integration smoke test
        public async Task<ConsumerResponseModel> TestCustomerTransactionProcessorService()
        {
            /* Test Process for CustomerTransactionProcessorService
             * 1) Get list of transactions
             * 2) Select 1 Id and fetch the single transaction using the function
             * 3) Insert the transaction and check if the Insert is successful
             * 4) Update the new transaction and check if the Update is successful
             * 5) Delete the transaction and check if the delete is successful
             */
            try
            {
                int userId = 1; // Assuming a userId for testing

                StringBuilder sb = new StringBuilder();

                //Step 1 Get the list of transactions
                DateTime startDate = new DateTime(2020, 1, 1), endDate = new DateTime(2026, 12, 31);
                int pageNumber = 1, pageSize = 10;

                PagedTransactionResultFromService transactions = await GetAllTransactionsWithPagination(startDate, endDate, pageNumber, pageSize, "");

                if (transactions.ListOfTransactions == null)
                {
                    return new ConsumerResponseModel()
                    {
                        Status = false,
                        Message = "Step 1: GetAll - Failed"
                    };
                }
                else
                {
                    sb.AppendLine("Step 1: GetAll - Passed");
                }

                //Step 2 
                CustomerTransactionDto transaction = await GetTransactionById(transactions.ListOfTransactions[0].Id);
                if (transaction == null)
                {
                    return new ConsumerResponseModel()
                    {
                        Status = false,
                        Message = "Step 2: GetById - Failed"
                    };
                }
                else
                {
                    sb.AppendLine("Step 2: GetById - Passed");
                }

                //Step 3
                CustomerTransactionDto newTransactionDto = transaction;

                CustomerTransactionModel transactionToAdd = TransactionMapper.DtoToEntity(newTransactionDto);

                transactionToAdd.Id = 0;

                int newId = await AddTransaction(newTransactionDto, userId);

                if (newId <= 0)
                {
                    return new ConsumerResponseModel()
                    {
                        Status = false,
                        Message = "Step 3: Add - Failed"
                    };
                }
                else
                {
                    sb.AppendLine("Step 3: Add - Passed");
                }

                //Step 4 Update the transaction
                int idToUpdate = newId;


                CustomerTransactionDto dtoToUpdate = await GetTransactionById(idToUpdate);

                CustomerTransactionModel modelToUpdate = TransactionMapper.DtoToEntity(dtoToUpdate);

                modelToUpdate.Amount += 10;

                bool updateResult = await UpdateTransaction(TransactionMapper.EntityToDto(modelToUpdate), userId);

                if (!updateResult)
                {
                    return new ConsumerResponseModel()
                    {
                        Status = false,
                        Message = "Step 4: Update - Failed"
                    };
                }
                else
                {
                    sb.AppendLine("Step 4: Update - Passed");
                }

                //Step 5 Delete the transaction
                bool deleteResult = await DeleteTransaction(TransactionMapper.EntityToDto(modelToUpdate), userId);


                CustomerTransactionDto deletedDto = await GetTransactionById(modelToUpdate.Id);

                if (!deleteResult || !deletedDto.IsDeleted)
                {
                    return new ConsumerResponseModel()
                    {
                        Status = false,
                        Message = "Step 5: Delete - Failed"
                    };
                }
                else
                {
                    sb.AppendLine("Step 5: Delete - Passed");
                }

                return new ConsumerResponseModel()
                {
                    Status = true,
                    Message = sb.ToString()
                };
            }
            catch (Exception ex)
            {
                return new ConsumerResponseModel()
                {
                    Message = "Test Failed",
                    Status = false,
                };
            }
        }
    }
}
