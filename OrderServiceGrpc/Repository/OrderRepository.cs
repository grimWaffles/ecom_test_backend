using Azure.Core;
using Dapper;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using OrderServiceGrpc.Helpers.cs;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Transactions;
using Z.Dapper.Plus;
using static Dapper.SqlMapper;

namespace OrderServiceGrpc.Repository
{
    public interface IOrderRepository
    {
        Task<OrderModel> GetOrderById(int orderId);
        Task<List<OrderModel>> GetAllOrders();
        Task<bool> AddOrder(OrderModel request, int userId);
        Task<bool> UpdateOrder(OrderModel request, List<OrderItemModel> addList, List<OrderItemModel> deleteList, List<OrderItemModel> updateList, int userId);
        Task<bool> DeleteOrder(int requestId, int userId);
        Task<int> GetOrderCount();
        Task<Tuple<int, int, List<OrderModel>>> GetAllOrdersWithPagination(OrderListRequest request);
        Task<List<OrderItemModel>> GetOrderItemsForOrder(int orderId);
        Task<RepoResponseModel> InsertOrderCreateEvent(int orderId);
    }

    public class OrderRepository : IOrderRepository
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;

        public OrderRepository(IConfiguration configuration)
        {
            _config = configuration;
            _connectionString = _config.GetSection("ConnectionStrings:DefaultConnection").Get<string>() ?? "";
        }

        public async Task<bool> AddOrder(OrderModel request, int userId)
        {
            const string insertOrderSql = @" INSERT INTO [dbo].[Orders]
                                   ([OrderDate] ,[OrderCounter] ,[UserId]  ,[Status] ,[NetAmount] ,[CreatedBy] ,[CreatedDate] ,[IsDeleted])
                             VALUES (@OrderDate,  @OrderCounter, @UserId, @Status, @NetAmount, @CreatedBy,  @CreatedDate, @IsDeleted ); select Convert(int,SCOPE_IDENTITY())";

            await using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using DbTransaction t = await conn.BeginTransactionAsync();

            try
            {
                DynamicParameters iop = new DynamicParameters();

                iop.Add("@OrderDate", request.OrderDate);
                iop.Add("@OrderCounter", request.OrderCounter);
                iop.Add("@UserId", request.UserId);
                iop.Add("@Status", request.Status);
                iop.Add("@NetAmount", request.NetAmount);

                iop.Add("@CreatedBy", userId);
                iop.Add("@CreatedDate", DateTime.UtcNow);
                iop.Add("@IsDeleted", false);

                int insertedOrderId = await conn.ExecuteScalarAsync<int>(insertOrderSql, iop, t);

                foreach (var item in request.OrderItems)
                {
                    item.OrderId = insertedOrderId;
                    item.CreatedDate = DateTime.UtcNow;
                    item.CreatedBy = userId;
                }

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, (SqlTransaction)t))
                {
                    bulkCopy.DestinationTableName = "OrderItems";

                    DataTable dt = DataTableConverter.ToDataTable<OrderItemModel>(request.OrderItems);

                    await bulkCopy.WriteToServerAsync(dt);
                }

                await t.CommitAsync();
            }
            catch (Exception e)
            {
                await t.RollbackAsync();
                return false;
            }

            return true;
        }

        public async Task<bool> DeleteOrder(int request, int userId)
        {
            try
            {
                const string sql = @"update OrderItems set IsDeleted = 1, ModifiedBy = @ModifiedBy, ModifiedDate = @ModifiedDate where OrderId = @OrderId;update Orders set IsDeleted = 1, ModifiedBy = @ModifiedBy, ModifiedDate = @ModifiedDate where Id = @OrderId";

                DynamicParameters parameters = new DynamicParameters();

                parameters.Add("@OrderId", request);
                parameters.Add("@ModifiedBy", userId);
                parameters.Add("@ModifiedDate", DateTime.UtcNow);

                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using DbTransaction transaction = await conn.BeginTransactionAsync();

                try
                {
                    await conn.ExecuteAsync(sql, parameters, transaction);

                    await transaction.CommitAsync();
                }
                catch (Exception e)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> UpdateOrder(OrderModel request, List<OrderItemModel> addList, List<OrderItemModel> deleteList, List<OrderItemModel> updateList, int userId)
        {
            const string updateOrderObjectSqlSingle = @"UPDATE Orders SET
                                                            OrderDate = @OrderDate,
                                                            UserId = @UserId,
                                                            Status = @Status,
                                                            NetAmount = @NetAmount,
                                                            ModifiedBy = @ModifiedBy,
                                                            ModifiedDate = @ModifiedDate
                                                        WHERE Id = @Id; ";


            const string deleteOrderItemSingleSql = @"  update o set
	                                                        o.ModifiedBy = @UserId,
	                                                        o.ModifiedDate = GETDATE(),
	                                                        o.IsDeleted = 1
                                                        from OrderItems o
                                                        where o.Id = @Id and o.OrderId = @OrderId";

            const string updateOrderItemSingleSql = @"  update o set
	                                                        o.Quantity = @Quantity,
	                                                        o.GrossAmount = @GrossAmount,
	                                                        o.ModifiedBy = @UserId,
	                                                        o.ModifiedDate = GETDATE()
                                                        from OrderItems o
                                                        where o.Id = @Id and o.OrderId = @OrderId";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var transaction = await conn.BeginTransactionAsync();

            try
            {
                //Delete List Processing
                foreach (var item in deleteList)
                {
                    var deleteParams = new DynamicParameters();
                    deleteParams.Add("@Id", item.Id);
                    deleteParams.Add("@UserId", userId);
                    deleteParams.Add("@OrderId", item.OrderId);

                    await conn.ExecuteAsync(deleteOrderItemSingleSql, deleteParams, transaction);
                }

                //AddList Processing
                if(addList.Count > 0)
                {
                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, (SqlTransaction)transaction))
                    {
                        bulkCopy.DestinationTableName = "OrderItems";

                        DataTable dt = DataTableConverter.ToDataTable<OrderItemModel>(addList);

                        await bulkCopy.WriteToServerAsync(dt);
                    }
                }

                //UpdateList processing
                foreach (var item in updateList)
                {
                    var updateParams = new DynamicParameters();
                    updateParams.Add("@Id", item.Id);
                    updateParams.Add("@Quantity", item.Quantity);
                    updateParams.Add("@GrossAmount", item.GrossAmount);
                    updateParams.Add("@UserId", userId);
                    updateParams.Add("@OrderId", item.OrderId);

                    await conn.ExecuteAsync(updateOrderItemSingleSql, updateParams, transaction);
                }

                //Main order object
                var orderParams = new DynamicParameters();

                orderParams.Add("@OrderDate", request.OrderDate);
                orderParams.Add("@UserId", request.UserId);
                orderParams.Add("@Status", request.Status);
                orderParams.Add("@NetAmount", request.NetAmount);
                orderParams.Add("@ModifiedBy", userId);
                orderParams.Add("@ModifiedDate", DateTime.UtcNow);
                orderParams.Add("@Id", request.Id);

                await conn.ExecuteAsync(updateOrderObjectSqlSingle, orderParams, transaction);

                //Commit the transaction
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<List<OrderModel>> GetAllOrders()
        {
            string sql = @" SELECT [Id]
                                  ,[OrderDate]
                                  ,[OrderCounter]
                                  ,[UserId]
                                  ,[Status]
                                  ,[NetAmount]
                                  ,[CreatedBy]
                                  ,[CreatedDate]
                                  ,[ModifiedDate]
                                  ,[ModifiedBy]
                                  ,[IsDeleted]
                              FROM [Orders] 
                              WHERE IsDeleted = 0
                              order by OrderDate desc";
            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                return (List<OrderModel>)await conn.QueryAsync<OrderModel>(sql);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<Tuple<int, int, List<OrderModel>>> GetAllOrdersWithPagination(OrderListRequest request)
        {
            const string sql = @"   declare @TotalOrders int, @TotalPages int
                                    select
	                                    o.*
                                    from Orders o 
									where o.OrderDate >= Convert(date,@StartDate) and o.OrderDate <= Convert(date,@EndDate)
                                    order by o.OrderDate desc
									OFFSET (@PageNumber - 1) * @PageSize ROWS
                                    fetch next @PageSize rows only

                                    select @TotalOrders = count(*) from Orders where OrderDate >=  Convert(date,@StartDate) and OrderDate <= Convert(date,@EndDate)
                                    select @TotalPages = CEILING(CAST(@TotalOrders as float)/ @PageSize)

                                    select @TotalPages TotalPages
									select @TotalOrders TotalOrders";
            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var parameters = new DynamicParameters();

                parameters.Add("PageNumber", request.PageNumber);
                parameters.Add("PageSize", request.PageSize);
                parameters.Add("StartDate", DateTimeHelper.ConvertTimestampToDateTime(request.StartDate));
                parameters.Add("EndDate", DateTimeHelper.ConvertTimestampToDateTime(request.EndDate));

                GridReader reader = await conn.QueryMultipleAsync(sql, parameters);

                List<OrderModel> orders = reader.Read<OrderModel>().ToList();
                int totalPages = reader.ReadSingle<int>();
                int totalOrders = reader.ReadSingle<int>();

                return Tuple.Create<int, int, List<OrderModel>>(totalPages, totalOrders, orders);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<OrderModel> GetOrderById(int orderId)
        {
            const string fetchOrderSql = @"select * from Orders where Id = @OrderId and IsDeleted = 0;
                                            select * from OrderItems where OrderId = @OrderId and IsDeleted = 0;";

            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                DynamicParameters parameters = new DynamicParameters();
                parameters.Add("@OrderId", orderId);

                GridReader reader = await conn.QueryMultipleAsync(fetchOrderSql, parameters);

                OrderModel model = reader.Read<OrderModel>().First();

                model.OrderItems = reader.Read<OrderItemModel>().ToList() ;

                return model;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<List<OrderItemModel>> GetOrderItemsForOrder(int orderId)
        {
            string sql = @"select * from OrderItems where OrderId = @OrderId and IsDeleted = 0";
            DynamicParameters dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@Id", orderId);
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    return (List<OrderItemModel>)await conn.QueryAsync<OrderItemModel>(sql, dynamicParameters);
                }
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<RepoResponseModel> InsertOrderCreateEvent(int orderId)
        {
            string sql = "Insert into OrderEventLogs(OrderId,CreatedAt) values(@OrderId,@CreatedAt)";
            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                try
                {
                    DynamicParameters iop = new DynamicParameters();

                    iop.Add("@OrderId", orderId);
                    iop.Add("@CreatedAt", DateTime.UtcNow);

                    await conn.ExecuteAsync(sql, iop);

                    Console.WriteLine($"Inserted OrderID:{orderId} successfully");

                    return new RepoResponseModel() { Status = true, Message = "Order inserted successfully", StackTrace = "" };
                }

                catch (Exception e) { Console.WriteLine(e.StackTrace); return new RepoResponseModel() { Status = false, Message = e.Message, StackTrace = e.StackTrace ?? "Stack trace unavailable" }; }

                finally { await conn.CloseAsync(); }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);

                return new RepoResponseModel() { Status = false, Message = e.Message, StackTrace = e.StackTrace ?? "Stack trace unavailable" };
            }
        }

        public async Task<int> GetOrderCount()
        {
            string sql = @"select Count(*) from Orders";
            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                return await conn.ExecuteScalarAsync<int>(sql);
            }
            catch (Exception e)
            {
                return 0;
            }
        }
    }
}
