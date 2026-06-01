using Dapper;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using OrderServiceGrpc.Models.ConfigModels;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using System.Collections.Immutable;
using static Dapper.SqlMapper;

namespace OrderServiceGrpc.Repository
{
    public interface ICustomerTransactionRepository
    {
        Task<CustomerTransactionModel> GetTransactionById(int id);
        Task<List<CustomerTransactionModel>> GetAllTransactions();
        Task<int> AddTransaction(CustomerTransactionModel request, int userId);
        Task<bool> UpdateTransaction(CustomerTransactionModel request, int userId);
        Task<bool> UpdateTransactionUsingOrderId(CustomerTransactionModel request, int userId);
        Task<bool> DeleteTransaction(CustomerTransactionModel request, int userId);
        Task<int> GetTransactionCount();
        Task<PagedTransactionResultFromRepo> GetAllTransactionsWithPagination(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string transactionType);
        Task<int> GetTotalTransactionCountForUser(int userId);
        Task<int> CheckIfTransactionKeyExists(string trxKey);
    }

    public class CustomerTransactionRepository : ICustomerTransactionRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<CustomerTransactionRepository> _logger;

        public CustomerTransactionRepository(IOptions<DatabaseConfig> dbConfig, IOptions<DatabaseConnection> connectionStrings, ILogger<CustomerTransactionRepository> logger)
        {
            _connectionString = (dbConfig.Value.Database.ToLower(), dbConfig.Value.Mode.ToLower()) switch
            {
                ("home", "local") => connectionStrings.Value.SqlServerHomeConnection,
                ("home", "docker") => connectionStrings.Value.SqlServerHomeDockerConnection,
                ("work", "local") => connectionStrings.Value.SqlServerWorkConnection,
                ("work", "docker") => connectionStrings.Value.SqlServerWorkDockerConnection,
                _ => ""
            };

            _logger = logger;
        }

        public async Task<int> AddTransaction(CustomerTransactionModel request, int userId)
        {
            string sql = @" INSERT INTO CustomerTransactions (
                                UserId,
                                TransactionType,
                                Amount,
                                CreatedDate,
                                CreatedBy,
                                IsDeleted,
                                TransactionDate,
                                TransactionKey,
                                OrderId
                            )
                            VALUES (
                                @UserId,
                                @TransactionType,
                                @Amount,
                                @CreatedDate,
                                @CreatedBy,
                                @IsDeleted,
                                @TransactionDate,
                                @TransactionKey,
                                @OrderId
                            ); select SCOPE_IDENTITY();";

            DynamicParameters parameters = new DynamicParameters();

            parameters.Add("@UserId", request.UserId);
            parameters.Add("@TransactionType", request.TransactionType);
            parameters.Add("@TransactionDate", DateTime.Now);
            parameters.Add("@Amount", request.Amount);
            parameters.Add("@CreatedDate", DateTime.Now);
            parameters.Add("@CreatedBy", userId);
            parameters.Add("@IsDeleted", false);
            parameters.Add("@TransactionKey", request.TransactionKey ?? "");
            parameters.Add("@OrderId", request.OrderId == 0 ? null : request.OrderId);

            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var tx = await conn.BeginTransactionAsync();

                try
                {
                    var result = await conn.ExecuteScalarAsync(sql, parameters, tx);
                    int newId = Convert.ToInt32(result);
                    await tx.CommitAsync();

                    _logger.LogInformation("AddTransaction: created transaction Id {TransactionId} for user {UserId}", newId, request.UserId);

                    return newId;
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogError(ex, "AddTransaction: failed to add transaction for user {UserId}", request.UserId);
                    return -1;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AddTransaction: database connection failed for user {UserId}", request.UserId);
                return -1;
            }
        }

        public async Task<bool> DeleteTransaction(CustomerTransactionModel request, int userId)
        {
            string sql = @" UPDATE CustomerTransactions
                            SET
                                IsDeleted = @IsDeleted,
                                ModifiedDate = @ModifiedDate,
                                ModifiedBy = @ModifiedBy,
                                TransactionKey = @TransactionKey
                            WHERE
                                Id = @Id;";

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@ModifiedDate", DateTime.Now);
            parameters.Add("@ModifiedBy", userId);
            parameters.Add("@IsDeleted", true);
            parameters.Add("@Id", request.Id);
            parameters.Add("@TransactionKey", request.TransactionKey);

            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var tx = await conn.BeginTransactionAsync();
                try
                {
                    int rows = await conn.ExecuteAsync(sql, parameters, tx);
                    await tx.CommitAsync();

                    bool success = rows > 0;
                    _logger.LogInformation("DeleteTransaction: TransactionId {Id}, deleted: {Success} by user {UserId}", request.Id, success, userId);
                    return success;
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogError(ex, "DeleteTransaction: failed for TransactionId {Id} by user {UserId}", request.Id, userId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteTransaction: database connection failed for TransactionId {Id}", request.Id);
                return false;
            }
        }

        public async Task<List<CustomerTransactionModel>> GetAllTransactions()
        {
            try
            {
                string sql = @"select * from CustomerTransactions";

                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var transactions = (await conn.QueryAsync<CustomerTransactionModel>(sql)).ToList();

                return transactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllTransactions: failed to retrieve transactions");
                return null;
            }
        }

        public async Task<int> GetTotalTransactionCountForUser(int userId)
        {
            try
            {
                string sql = @" select Count(*) TotalTransactionsToday from CustomerTransactions where UserId = @UserId and Convert(date,TransactionDate) = Convert(date,GETDATE());";

                DynamicParameters parameters = new DynamicParameters();
                parameters.Add("@UserId", userId);

                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                int totalTransactionsToday = await conn.ExecuteScalarAsync<int>(sql, parameters);

                return totalTransactionsToday;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTotalTransactionCountForUser: failed for user {UserId}", userId);
                return -1;
            }
        }

        public async Task<int> CheckIfTransactionKeyExists(string trxKey)
        {
            try
            {
                string sql = @" select Count(*) TotalTransactionsToday from CustomerTransactions where TransactionKey = @TransactionKey and IsDeleted = 0";

                DynamicParameters parameters = new DynamicParameters();
                parameters.Add("@TransactionKey", trxKey);

                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                int trxCount = await conn.ExecuteScalarAsync<int>(sql, parameters);

                return trxCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckIfTransactionKeyExists: failed for key {TransactionKey}", trxKey);
                return -1;
            }
        }

        public async Task<PagedTransactionResultFromRepo> GetAllTransactionsWithPagination(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string transactionType)
        {
            PagedTransactionResultFromRepo result = new PagedTransactionResultFromRepo();

            try
            {
                string sql = @"	SELECT 
		                            [Id]
		                            ,[UserId]
		                            ,[TransactionType]
		                            ,[Amount]
		                            ,[CreatedBy]
		                            ,[CreatedDate]
		                            ,[ModifiedDate]
		                            ,[ModifiedBy]
		                            ,[IsDeleted]
		                            ,[TransactionDate]
                                    ,[TransactionKey]
                                    ,[OrderId]
	                            FROM [ECommercePlatform].[dbo].[CustomerTransactions]
	                            WHERE
									(@TransactionType = '' or TransactionType = @TransactionType) and
									convert(date,TransactionDate) >= @StartDate and
									convert(date,TransactionDate) <= @EndDate and
                                    IsDeleted = 0
	                            ORDER BY TransactionDate desc
	                            OFFSET (@PageNumber-1)*(@PageSize) ROWS
	                            FETCH NEXT @PageSize ROWS ONLY

	                            declare @TRows int = (SELECT COUNT(*) TotalTransactions FROM CustomerTransactions where (@TransactionType = '' or TransactionType = @TransactionType) and
									convert(date,TransactionDate) >= @StartDate and
									convert(date,TransactionDate) <= @EndDate )
	                            
								declare @TPages int = @TRows/@PageSize 

	                            select @TRows TRows
	                            select @TPages + 1 TPages";

                DynamicParameters parameters = new DynamicParameters();

                parameters.Add("@StartDate", startDate);
                parameters.Add("@EndDate", endDate);
                parameters.Add("@PageNumber", pageNumber);
                parameters.Add("@PageSize", pageSize);
                parameters.Add("@TransactionType", transactionType);

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    GridReader resultSet = await conn.QueryMultipleAsync(sql, parameters);

                    List<CustomerTransactionModel> list = (List<CustomerTransactionModel>)await resultSet.ReadAsync<CustomerTransactionModel>();
                    int totalRows = await resultSet.ReadSingleAsync<int>();
                    int totalPages = await resultSet.ReadSingleAsync<int>();

                    result.Status = true;
                    result.ErrorMessage = "";
                    result.TotalPages = totalPages;
                    result.TotalTransactions = totalRows;
                    result.ListOfTransactions = list;

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllTransactionsWithPagination: failed between {StartDate} and {EndDate}", startDate, endDate);
                return new PagedTransactionResultFromRepo()
                {
                    Status = false,
                    ErrorMessage = "Failed to fetch orders",
                };
            }
        }

        public async Task<CustomerTransactionModel> GetTransactionById(int id)
        {
            try
            {
                await using var db = new SqlConnection(_connectionString);
                await db.OpenAsync();

                string sql = @" select 
                                    *--TransactionType, Amount, TransactionDate, UserId 
                                from CustomerTransactions 
                                where Id = @Id and IsDeleted = 0";

                DynamicParameters param = new DynamicParameters();
                param.Add("@Id", id);

                CustomerTransactionModel model = await db.QuerySingleAsync<CustomerTransactionModel>(sql, param);

                return model;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTransactionById: failed for Id {Id}", id);
                return null;
            }
        }

        public async Task<int> GetTransactionCount()
        {
            try
            {
                await using var db = new SqlConnection(_connectionString);
                string sql = "select count(*) from CustomerTransactions";

                await db.OpenAsync();
                int response = await db.ExecuteScalarAsync<int>(sql);


                return response;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTransactionCount: failed");
                return 0;
            }
        }

        public async Task<bool> UpdateTransaction(CustomerTransactionModel request, int userId)
        {
            string sql = @" UPDATE CustomerTransactions
                            SET
                                UserId = @UserId,
                                TransactionType = @TransactionType,
                                Amount = @Amount,
                                IsDeleted = @IsDeleted,
                                TransactionDate = @TransactionDate,
                                ModifiedDate = @ModifiedDate,
                                ModifiedBy = @ModifiedBy,
                                OrderId = @OrderId
                            WHERE
                                Id = @Id;";

            DynamicParameters parameters = new DynamicParameters();

            parameters.Add("@Id", request.Id);
            parameters.Add("@UserId", request.UserId);
            parameters.Add("@TransactionType", request.TransactionType);
            parameters.Add("@TransactionDate", request.TransactionDate);
            parameters.Add("@Amount", request.Amount);
            parameters.Add("@ModifiedDate", DateTime.Now);
            parameters.Add("@ModifiedBy", userId);
            parameters.Add("@IsDeleted", false);
            parameters.Add("@OrderId", request.OrderId == 0 ? null : request.OrderId);

            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var tx = await conn.BeginTransactionAsync();
                try
                {
                    int rows = await conn.ExecuteAsync(sql, parameters, tx);
                    await tx.CommitAsync();

                    bool success = rows > 0;
                    _logger.LogInformation("UpdateTransaction: TransactionId {Id} update success: {Success} by user {UserId}", request.Id, success, userId);
                    return success;
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogError(ex, "UpdateTransaction: failed for TransactionId {Id} by user {UserId}", request.Id, userId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateTransaction: database connection failed for TransactionId {Id}", request.Id);
                return false;
            }
        }

        public async Task<bool> UpdateTransactionUsingOrderId(CustomerTransactionModel request, int userId)
        {
            string sql = @" UPDATE CustomerTransactions
                            SET
                                UserId = @UserId,
                                TransactionType = @TransactionType,
                                Amount = @Amount,
                                IsDeleted = @IsDeleted,
                                TransactionDate = @TransactionDate,
                                ModifiedDate = @ModifiedDate,
                                ModifiedBy = @ModifiedBy,
                                OrderId = @OrderId
                            WHERE
                                OrderId = @Id;";

            DynamicParameters parameters = new DynamicParameters();

            parameters.Add("@UserId", request.UserId);
            parameters.Add("@TransactionType", request.TransactionType);
            parameters.Add("@TransactionDate", request.TransactionDate);
            parameters.Add("@Amount", request.Amount);
            parameters.Add("@ModifiedDate", DateTime.Now);
            parameters.Add("@ModifiedBy", userId);
            parameters.Add("@IsDeleted", false);
            parameters.Add("@OrderId", request.OrderId);

            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var tx = await conn.BeginTransactionAsync();
                try
                {
                    int rows = await conn.ExecuteAsync(sql, parameters, tx);
                    await tx.CommitAsync();

                    bool success = rows > 0;
                    _logger.LogInformation("UpdateTransaction: TransactionId {Id} update success: {Success} by user {UserId}", request.Id, success, userId);
                    return success;
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogError(ex, "UpdateTransaction: failed for TransactionId {Id} by user {UserId}", request.Id, userId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateTransaction: database connection failed for TransactionId {Id}", request.Id);
                return false;
            }
        }
    }
}
