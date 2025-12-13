using Dapper;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.ObjectPool;
using OrderServiceGrpc.Helpers.cs;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using System.Collections.Immutable;
using static Dapper.SqlMapper;

namespace OrderServiceGrpc.Repository
{
    public interface ICustomerTransactionRepository
    {
        Task<CustomerTransactionModel> GetTransactionById(int id);
        Task<List<CustomerTransactionModel>> GetAllTransactions(TransactionRequestMultiple request);
        Task<bool> AddTransaction(TransactionDto request, int userId);
        Task<bool> UpdateTransaction(TransactionDto request, int userId);
        Task<bool> DeleteTransaction(TransactionDto request, int userId);
        Task<int> GetTransactionCount();
        Task<TransactionResponseMultiple> GetAllTransactionsWithPagination(TransactionRequestMultiple request);
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
        public async Task<bool> AddTransaction(TransactionDto request, int userId)
        {
            string sql = @" INSERT INTO CustomerTransactionModel (
                                UserId,
                                TransactionType,
                                Amount,
                                CreatedDate,
                                CreatedBy,
                                IsDeleted,
                                TransactionDate
                            )
                            VALUES (
                                @UserId,
                                @TransactionType,
                                @Amount,
                                @CreatedDate,
                                @CreatedBy,
                                @IsDeleted,
                                @TransactionDate
                            );";
            //object[] parameters = { new
            //{
            //    UserId = request.UserId,
            //    TransactionType = request.TransactionType,
            //    Amount = request.Amount,
            //    CreatedDate = DateTime.Now,
            //    CreatedBy = userId,
            //    IsDeleted = false,
            //    TransactionDate = DateTime.Now
            //}};

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@UserId", request.UserId);
            parameters.Add("TransactionType", request.TransactionType);
            parameters.Add("@TransactionDate", DateTimeHelper.ConvertTimestampToDateTime(request.TransactionDate));
            parameters.Add("@Amount", request.Amount);
            parameters.Add("@CreatedDate", DateTimeHelper.ConvertTimestampToDateTime(DateTimeHelper.ConvertDateTimeToTimestamp(DateTime.Now)));
            parameters.Add("@CreatedBy", userId);
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

        public async Task<bool> DeleteTransaction(TransactionDto request, int userId)
        {
            string sql = @" UPDATE CustomerTransactionModel
                            SET
                                IsDeleted = @IsDeleted,
                                ModifiedDate = @ModifiedDate,
                                ModifiedBy = @ModifiedBy
                            WHERE
                                Id = @Id;";

            DynamicParameters parameters = new DynamicParameters();

            parameters.Add("@ModifiedDate", DateTimeHelper.ConvertTimestampToDateTime(DateTimeHelper.ConvertDateTimeToTimestamp(DateTime.Now)));
            parameters.Add("@ModifiedBy", userId);
            parameters.Add("@IsDeleted", false);
            parameters.Add("@Id", request.Id);

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

        public async Task<List<CustomerTransactionModel>> GetAllTransactions(TransactionRequestMultiple request)
        {
            try
            {
                string sql = @"select * from CustomerTransactions";

                SqlConnection conn = new SqlConnection(_connectionString);

                await conn.OpenAsync();
                List<CustomerTransactionModel> transactions = (List<CustomerTransactionModel>)await conn.QueryAsync<CustomerTransactionModel>(sql);
                await conn.CloseAsync();

                return transactions;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<TransactionResponseMultiple> GetAllTransactionsWithPagination(TransactionRequestMultiple request)
        {
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

                parameters.Add("@StartDate", DateTimeHelper.ConvertTimestampToDateTime(request.StartDate));
                parameters.Add("@EndDate", DateTimeHelper.ConvertTimestampToDateTime(request.EndDate));
                parameters.Add("@PageNumber", Convert.ToInt32(request.PageNumber));
                parameters.Add("@PageSize", Convert.ToInt32(request.PageLength));
                parameters.Add("@TransactionType", Convert.ToString(request.TransactionType));

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    GridReader resultSet = await conn.QueryMultipleAsync(sql, parameters);

                    List<CustomerTransactionModel> list = (List<CustomerTransactionModel>)await resultSet.ReadAsync<CustomerTransactionModel>();
                    int totalRows = await resultSet.ReadSingleAsync<int>();
                    int totalPages = await resultSet.ReadSingleAsync<int>();

                    TransactionResponseMultiple response = new TransactionResponseMultiple()
                    {
                        Status = true,
                        ErrorMessage = "",
                        TotalPages = totalPages,
                        TotalRows = totalRows,
                    };

                    response.Transactions.AddRange(list.Select(x => new TransactionDto()
                    {
                        Id = x.Id,
                        UserId = x.UserId,
                        TransactionType = x.TransactionType,
                        Amount = (double)x.Amount,
                        CreatedBy = x.CreatedBy,
                        CreatedDate = DateTimeHelper.ConvertDateTimeToTimestamp(x.CreatedDate),
                        IsDeleted = x.IsDeleted,
                        TransactionDate = DateTimeHelper.ConvertDateTimeToTimestamp(x.TransactionDate),
                        ModifiedBy = x.ModifiedBy,
                        ModifiedDate = DateTimeHelper.ConvertDateTimeToTimestamp(x.ModifiedDate),
                    }).ToList());

                    return response;
                }
            }
            catch (Exception e)
            {
                return new TransactionResponseMultiple()
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

                    DynamicParameters p =new DynamicParameters();
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
                using (var db = new SqlConnection(_connectionString))
                {
                    string sql = "select count(*) from CustomerTransactions";
                    await db.OpenAsync();
                    return await db.ExecuteScalarAsync<int>(sql);
                    await db.CloseAsync();
                }

            }
            catch (Exception e)
            {
                return 0;
            }
        }

        public async Task<bool> UpdateTransaction(TransactionDto request, int userId)
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

            //object[] parameters = { new
            //{
            //    Id=request.Id,
            //    UserId = request.UserId,
            //    TransactionType = request.TransactionType,
            //    Amount = request.Amount,
            //    ModifiedDate = DateTime.Now,
            //    ModifiedBy = userId,
            //    IsDeleted = false,
            //    TransactionDate = DateTime.Now
            //}};

            DynamicParameters parameters = new DynamicParameters();

            parameters.Add("@Id", request.Id);
            parameters.Add("@UserId", request.UserId);
            parameters.Add("TransactionType", request.TransactionType);
            parameters.Add("@TransactionDate", DateTimeHelper.ConvertTimestampToDateTime(request.TransactionDate));
            parameters.Add("@Amount", request.Amount);
            parameters.Add("@ModifiedDate", DateTimeHelper.ConvertTimestampToDateTime(DateTimeHelper.ConvertDateTimeToTimestamp(DateTime.Now)));
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
