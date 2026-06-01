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
    public interface ICustomerTransactionService
    {
        Task<CustomerTransactionDto> GetTransactionById(int id);
        //Task<List<CustomerTransactionDto>> GetAllTransactions();
        Task<int> AddTransaction(CustomerTransactionDto request, int userId);
        Task<bool> UpdateTransaction(CustomerTransactionDto request, int userId);
        Task<bool> UpdateTransactionUsingOrderId(CustomerTransactionDto request, int userId);
        Task<bool> DeleteTransaction(CustomerTransactionDto request, int userId);
        Task<PagedTransactionResultFromService> GetAllTransactionsWithPagination(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string transactionType);
        Task<ConsumerResponseModel> TestCustomerTransactionProcessorService();
    }

    public class CustomerTransactionService : ICustomerTransactionService
    {
        private readonly ICustomerTransactionRepository _repo;
        private readonly ILogger<CustomerTransactionService> _logger;

        public CustomerTransactionService(ILogger<CustomerTransactionService> logger,ICustomerTransactionRepository repo)
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

        //public async Task<List<CustomerTransactionDto>> GetAllTransactions()
        //{
        //    try
        //    {
        //        List<CustomerTransactionModel> list = await _repo.GetAllTransactions();

        //        if (list == null)
        //        {
        //            return null;
        //        }

        //        return list.Select(x => TransactionMapper.EntityToDto(x)).ToList();
        //    }
        //    catch (Exception ex)
        //    {
        //        return null;
        //    }
        //}

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

        public async Task<bool> UpdateTransactionUsingOrderId(CustomerTransactionDto request, int userId)
        {
            try
            {
                bool result = await _repo.UpdateTransactionUsingOrderId(TransactionMapper.DtoToEntity(request), userId);

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
            StringBuilder testLog = new StringBuilder();
            testLog.AppendLine("=== Customer Transaction Integration Test Started ===");

            DateTime startDate = new DateTime(2021, 01, 01);
            DateTime endDate = new DateTime(2026, 12, 31);
            int pageSize = 5, pageNumber = 1;
            int testUserId = 1;
            string transactionType = "DEPOSIT"; // adjust to a valid type in your system

            // ----------------------------------------------------------------
            // Step 1: Get paginated list of transactions
            // ----------------------------------------------------------------
            PagedTransactionResultFromService step1Response = await GetAllTransactionsWithPagination(
                startDate, endDate, pageNumber, pageSize, transactionType);

            if (step1Response == null || !step1Response.Status)
            {
                testLog.AppendLine("STEP 1: Failed to fetch transaction list");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            if (step1Response.ListOfTransactions == null || !step1Response.ListOfTransactions.Any())
            {
                testLog.AppendLine("STEP 1: No transactions found");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            testLog.AppendLine("STEP 1: Passed");

            // ----------------------------------------------------------------
            // Step 2: Get a single transaction by ID
            // ----------------------------------------------------------------
            CustomerTransactionDto objToSearch = step1Response.ListOfTransactions.FirstOrDefault();

            if (objToSearch == null || objToSearch.Id == 0)
            {
                testLog.AppendLine("STEP 2: No valid transaction found to fetch by ID");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            CustomerTransactionDto step2Response = await GetTransactionById(objToSearch.Id);

            if (step2Response == null)
            {
                testLog.AppendLine("STEP 2: Failed to fetch transaction by ID");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            testLog.AppendLine("STEP 2: Passed");

            // ----------------------------------------------------------------
            // Step 3: Create a new transaction (clone from fetched, reset ID)
            // ----------------------------------------------------------------
            CustomerTransactionDto modelToCreate = new CustomerTransactionDto()
            {
                UserId = step2Response.UserId,
                TransactionType = step2Response.TransactionType,
                TransactionDate = DateTime.UtcNow, // fresh date to generate a unique key
                Amount = step2Response.Amount
                // leave Id and TransactionKey unset — repo generates them
            };

            int insertedTransactionId = await AddTransaction(modelToCreate, testUserId);

            if (insertedTransactionId <= 0)
            {
                testLog.AppendLine("STEP 3: Failed to create transaction");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            // Verify the insert by reading it back
            CustomerTransactionDto insertedTransaction = await GetTransactionById(insertedTransactionId);

            if (insertedTransaction == null)
            {
                testLog.AppendLine("STEP 3: Failed to verify inserted transaction");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            testLog.AppendLine("STEP 3: Passed");

            // ----------------------------------------------------------------
            // Step 4: Update the newly created transaction
            // ----------------------------------------------------------------
            CustomerTransactionDto modelToUpdate = insertedTransaction;
            decimal originalAmount = modelToUpdate.Amount;
            modelToUpdate.Amount += 100; // apply a detectable change

            bool updateResult = await UpdateTransaction(modelToUpdate, testUserId);

            if (!updateResult)
            {
                testLog.AppendLine("STEP 4: Failed to update transaction");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            // Verify the update by reading it back
            CustomerTransactionDto updatedTransaction = await GetTransactionById(insertedTransactionId);

            if (updatedTransaction == null || updatedTransaction.Amount <= originalAmount)
            {
                testLog.AppendLine("STEP 4: Failed to verify updated transaction amount");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            testLog.AppendLine("STEP 4: Passed");

            // ----------------------------------------------------------------
            // Step 5: Delete the transaction
            // ----------------------------------------------------------------
            bool deleteResult = await DeleteTransaction(updatedTransaction, testUserId);

            if (!deleteResult)
            {
                testLog.AppendLine("STEP 5: Failed to delete transaction");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            // Verify deletion — should no longer be retrievable
            CustomerTransactionDto deletedCheck = await GetTransactionById(insertedTransactionId);

            if (deletedCheck != null)
            {
                testLog.AppendLine("STEP 5: Transaction still exists after delete — verification failed");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            testLog.AppendLine("STEP 5: Passed");

            // ----------------------------------------------------------------
            // All steps complete
            // ----------------------------------------------------------------
            testLog.AppendLine("=== Customer Transaction Integration Test Finished ===");

            return new ConsumerResponseModel()
            {
                Status = true,
                Message = testLog.ToString()
            };
        }
    }
}
