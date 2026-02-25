using Dapper;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.ObjectPool;
using OrderServiceGrpc.Helpers.cs;
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
        Task<bool> AddTransaction(CustomerTransactionModel request, int userId);
        Task<bool> UpdateTransaction(CustomerTransactionModel request, int userId);
        Task<bool> DeleteTransaction(CustomerTransactionModel request, int userId);
        Task<int> GetTransactionCount();
        Task<PagedTransactionResultRepo> GetAllTransactionsWithPagination(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string transactionType);
    }

    public class CustomerTransactionRepository : ICustomerTransactionRepository
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;

        public CustomerTransactionRepository(IConfiguration configuration)
        {
            _config = configuration;
            _connectionString = _config.GetSection("ConnectionStrings:DefaultConnection").Get<string>() ?? "";
        }
        public async Task<bool> AddTransaction(CustomerTransactionModel request, int userId)
        {
            string sql = @" INSERT INTO CustomerTransactionModel (
                                UserId,
                                TransactionType,
                                Amount,
                                CreatedDate,
                                CreatedBy,
                                IsDeleted,
                                TransactionDate, TransactionKey
                            )
                            VALUES (
                                @UserId,
                                @TransactionType,
                                @Amount,
                                @CreatedDate,
                                @CreatedBy,
                                @IsDeleted,
                                @TransactionDate,
                                @TransactionKey
                            );";

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@UserId", request.UserId);
            parameters.Add("TransactionType", request.TransactionType);
            parameters.Add("@TransactionDate", request.TransactionDate);
            parameters.Add("@Amount", request.Amount);
            parameters.Add("@CreatedDate", DateTime.Now);
            parameters.Add("@CreatedBy", userId);
            parameters.Add("@IsDeleted", false);
            parameters.Add("@TransactionKey", request.TransactionKey);

            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await conn.ExecuteAsync(sql, parameters);

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public async Task<bool> DeleteTransaction(CustomerTransactionModel request, int userId)
        {
            string sql = @" UPDATE CustomerTransactionModel
                            SET
                                IsDeleted = @IsDeleted,
                                ModifiedDate = @ModifiedDate,
                                ModifiedBy = @ModifiedBy
                            WHERE
                                Id = @Id;";

            DynamicParameters parameters = new DynamicParameters();

            parameters.Add("@ModifiedDate", DateTime.Now);
            parameters.Add("@ModifiedBy", userId);
            parameters.Add("@IsDeleted", false);
            parameters.Add("@Id", request.Id);

            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await conn.ExecuteAsync(sql, parameters);

                return true;
            }
            catch (Exception e)
            {
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
                List<CustomerTransactionModel> transactions = (List<CustomerTransactionModel>)await conn.QueryAsync<CustomerTransactionModel>(sql);

                return transactions;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<PagedTransactionResultRepo> GetAllTransactionsWithPagination(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string transactionType)
        {
            PagedTransactionResultRepo result = new PagedTransactionResultRepo();

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
	                            FROM [ECommercePlatform].[dbo].[CustomerTransactions]
	                            WHERE
									(@TransactionType = '' or TransactionType = @TransactionType) and
									convert(date,TransactionDate) >= @StartDate and
									convert(date,TransactionDate) <= @EndDate
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
            catch (Exception e)
            {
                return new PagedTransactionResultRepo()
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
                using (var db = new SqlConnection(_connectionString))
                {
                    string sql = @" select 
                                        *--TransactionType, Amount, TransactionDate, UserId 
                                    from CustomerTransactions 
                                    where Id = @Id";

                    DynamicParameters p = new DynamicParameters();
                    p.Add("@Id", id);

                    await db.OpenAsync();

                    CustomerTransactionModel model = await db.QuerySingleAsync<CustomerTransactionModel>(sql, p);
                    await db.DisposeAsync();

                    await db.CloseAsync();

                    return model;
                }
            }
            catch (Exception e)
            {
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
            catch (Exception e)
            {
                return 0;
            }
        }

        public async Task<bool> UpdateTransaction(CustomerTransactionModel request, int userId)
        {
            string sql = @" UPDATE CustomerTransactionModel
                            SET
                                UserId = @UserId,
                                TransactionType = @TransactionType,
                                Amount = @Amount,
                                IsDeleted = @IsDeleted,
                                TransactionDate = @TransactionDate,
                                ModifiedDate = @ModifiedDate,
                                ModifiedBy = @ModifiedBy
                            WHERE
                                Id = @Id;";

            DynamicParameters parameters = new DynamicParameters();

            parameters.Add("@Id", request.Id);
            parameters.Add("@UserId", request.UserId);
            parameters.Add("TransactionType", request.TransactionType);
            parameters.Add("@TransactionDate", request.TransactionDate);
            parameters.Add("@Amount", request.Amount);
            parameters.Add("@ModifiedDate", DateTime.Now);
            parameters.Add("@ModifiedBy", userId);
            parameters.Add("@IsDeleted", false);

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    await conn.ExecuteAsync(sql, parameters);
                    await conn.CloseAsync();
                    return true;
                }
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
