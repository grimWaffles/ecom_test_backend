using MySqlX.XDevAPI.Common;
using OrderServiceGrpc.Helpers;
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
        Task<TransactionProcessorResponseModel> GetTransactionById(int id);
        Task<TransactionProcessorResponseModel> GetAllTransactions();
        Task<TransactionProcessorResponseModel> AddTransaction(CustomerTransactionDto request, int userId);
        Task<TransactionProcessorResponseModel> UpdateTransaction(CustomerTransactionDto request, int userId);
        Task<TransactionProcessorResponseModel> DeleteTransaction(CustomerTransactionDto request, int userId);
        Task<TransactionProcessorResponseModel> GetAllTransactionsWithPagination(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string transactionType);
        Task<TransactionProcessorResponseModel> TestCustomerTransactionProcessorService();
    }

    public class CustomerTransactionProcessorService : ICustomerTransactionProcessorService
    {
        private readonly ICustomerTransactionRepository _repo;

        public CustomerTransactionProcessorService(ICustomerTransactionRepository repo)
        {
            _repo = repo;
        }

        public async Task<TransactionProcessorResponseModel> AddTransaction(CustomerTransactionDto request, int userId)
        {
            int result = -1;
            try
            {
                CustomerTransactionModel model = TransactionMapper.DtoToEntity(request);
                model.TransactionKey = GenerateTransactionKey(model);

                result = await _repo.AddTransaction(model, userId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}. StackTrace: {ex.StackTrace}");
            }

            return new TransactionProcessorResponseModel()
            {
                InsertedTrxId = result,
                Status = result > 0 ? true : false,
                Message = result > 0 ? "Success" : "Failed to insert transaction"
            };
        }

        private string GenerateTransactionKey(CustomerTransactionModel model)
        {
            return $"{model.UserId}-{model.TransactionType}-{model.TransactionDate.ToString("yyyyMMdd")}-00";
        }

        public async Task<TransactionProcessorResponseModel> DeleteTransaction(CustomerTransactionDto request, int userId)
        {
            bool result = false;
            try
            {
                CustomerTransactionModel model = TransactionMapper.DtoToEntity(request);
                model.TransactionKey =  model.TransactionKey.Substring(0, model.TransactionKey.Length-2) + "01";
                result = await _repo.DeleteTransaction(model, userId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}. StackTrace: {ex.StackTrace}");
            }

            return new TransactionProcessorResponseModel()
            {
                Status = result,
                Message = result ? "Success" : "Failed to insert transaction"
            };
        }

        public async Task<TransactionProcessorResponseModel> GetAllTransactions()
        {
            try
            {
                List<CustomerTransactionModel> list = await _repo.GetAllTransactions();

                if (list == null)
                {
                    return null;
                }

                return new TransactionProcessorResponseModel()
                {
                    ListOfTransactions = list.Select(x => TransactionMapper.EntityToDto(x)).ToList(),
                    Status = true,
                    Message = "Success"
                };
            }
            catch (Exception ex)
            {
                return new TransactionProcessorResponseModel()
                {
                    ListOfTransactions = null,
                    Status = false,
                    Message = "Failed to get transactions"
                };
            }
        }

        public async Task<TransactionProcessorResponseModel> GetAllTransactionsWithPagination(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string transactionType)
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

                return new TransactionProcessorResponseModel()
                {
                    PagedTrxResults = result,
                    Status = result.Status,
                    Message = result.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                return new TransactionProcessorResponseModel()
                {
                    PagedTrxResults = null,
                    Status = false,
                    Message = "Failed to get transactions"
                };
            }
        }

        public async Task<TransactionProcessorResponseModel> GetTransactionById(int id)
        {
            try
            {
                CustomerTransactionModel model = await _repo.GetTransactionById(id);

                return new TransactionProcessorResponseModel()
                {
                    ListOfTransactions = new List<CustomerTransactionDto>() { TransactionMapper.EntityToDto(model) },
                    Status = true,
                    Message = "Success"
                };
            }
            catch (Exception ex)
            {
                return new TransactionProcessorResponseModel()
                {
                    ListOfTransactions = null,
                    Status = false,
                    Message = "Failed to fetch data"
                };
            }
        }

        public async Task<TransactionProcessorResponseModel> UpdateTransaction(CustomerTransactionDto request, int userId)
        {
            try
            {
                bool result = await _repo.UpdateTransaction(TransactionMapper.DtoToEntity(request), userId);

                return new TransactionProcessorResponseModel()
                {
                    Status = result,
                    Message = result ? "Success" : "Failed to update transaction"
                };

            }
            catch (Exception ex)
            {
                return new TransactionProcessorResponseModel()
                {
                    Status = false,
                    Message = "Failed to update transaction"
                };
            }
        }

        //Integration smoke test
        public async Task<TransactionProcessorResponseModel> TestCustomerTransactionProcessorService()
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

                TransactionProcessorResponseModel transactions = await GetAllTransactionsWithPagination(startDate, endDate, pageNumber, pageSize, "");

                if (transactions.ListOfTransactions == null)
                {
                    return new TransactionProcessorResponseModel()
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
                TransactionProcessorResponseModel transaction = await GetTransactionById(transactions.ListOfTransactions[0].Id);
                if (transaction == null)
                {
                    return new TransactionProcessorResponseModel()
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
                CustomerTransactionDto newTransactionDto = transaction.ListOfTransactions[0];

                CustomerTransactionModel transactionToAdd = TransactionMapper.DtoToEntity(newTransactionDto);

                transactionToAdd.Id = 0;
                TransactionProcessorResponseModel response = await AddTransaction(newTransactionDto, userId);

                int newId = response.InsertedTrxId;

                if (newId <= 0)
                {
                    return new TransactionProcessorResponseModel()
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

                TransactionProcessorResponseModel trxToUpdate = await GetTransactionById(idToUpdate);

                CustomerTransactionDto dtoToUpdate = trxToUpdate.ListOfTransactions[0];

                CustomerTransactionModel modelToUpdate = TransactionMapper.DtoToEntity(dtoToUpdate);

                modelToUpdate.Amount += 10;

                TransactionProcessorResponseModel repoResultForUpdate = await UpdateTransaction(TransactionMapper.EntityToDto(modelToUpdate), userId);

                bool updateResult = repoResultForUpdate.Status;

                if (!updateResult)
                {
                    return new TransactionProcessorResponseModel()
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
                TransactionProcessorResponseModel deleteResponse = await DeleteTransaction(TransactionMapper.EntityToDto(modelToUpdate), userId);
                bool deleteResult = deleteResponse.Status;

                TransactionProcessorResponseModel deleteDtoResponse = await GetTransactionById(modelToUpdate.Id);
                CustomerTransactionDto deletedDto = deleteResponse.ListOfTransactions[0];
                
                if (!deleteResult || !deletedDto.IsDeleted)
                {
                    return new TransactionProcessorResponseModel()
                    {
                        Status = false,
                        Message = "Step 5: Delete - Failed"
                    };
                }
                else
                {
                    sb.AppendLine("Step 5: Delete - Passed");
                }

                return new TransactionProcessorResponseModel()
                {
                    Status = true,
                    Message = sb.ToString()
                };
            }
            catch (Exception ex)
            {
                return new TransactionProcessorResponseModel()
                {
                    Message = "Test Failed",
                    Status = false,
                };
            }
        }
    }
}
