using Azure.Core;
using Dapper;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using OrderServiceGrpc.Helpers.cs;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.ConfigModels;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Transactions;
using static Dapper.SqlMapper;

namespace OrderServiceGrpc.Repository
{
    public interface IOrderRepository
    {
        Task<OrderModel> GetOrderById(int orderId);
        Task<int> AddSingleOrder(OrderModel request, int userId);
        Task<bool> UpdateOrder(OrderModel request, List<OrderItemModel> addList, List<OrderItemModel> deleteList, List<OrderItemModel> updateList, int userId);
        Task<bool> DeleteSingleOrder(int requestId, int userId);
        Task<int> GetOrderCount();
        Task<PagedOrderListModel> GetAllOrdersWithPagination(DateTime startDate, DateTime endDate, int pageSize, int pageNumber, int userId);
        Task<List<OrderItemModel>> GetOrderItemsForOrder(int orderId);
        Task<ConsumerResponseModel> InsertOrderCreateEvent(int orderId);
    }

    public class OrderRepository : IOrderRepository
    {
        private readonly string _connectionString;

        public OrderRepository(IOptions<DatabaseConfig> dbConfig, IOptions<DatabaseConnection> connectionStrings)
        {
            _connectionString = (dbConfig.Value.Database.ToLower(),dbConfig.Value.Mode.ToLower()) switch
            {
                ("home", "local") => connectionStrings.Value.SqlServerHomeConnection,
                ("home", "docker") => connectionStrings.Value.SqlServerHomeDockerConnection,
                ("work", "local") => connectionStrings.Value.SqlServerWorkConnection,
                ("work", "docker") => connectionStrings.Value.SqlServerWorkDockerConnection,
                _ => ""
            };
        }

        public async Task<int> AddSingleOrder(OrderModel request, int userId)
        {
            int insertedOrderId = 0;
            const string insertOrderSql = @" INSERT INTO [dbo].[Orders]
                                   ([OrderDate] ,[OrderCounter] ,[UserId]  ,[Status] ,[NetAmount] ,[CreatedBy] ,[CreatedDate] ,[IsDeleted],[ModifiedDate],[ModifiedBy])
                             VALUES (@OrderDate,  @OrderCounter, @UserId, @Status, @NetAmount, @CreatedBy,  @CreatedDate, @IsDeleted, @ModifiedDate,@ModifiedBy ); select Convert(int,SCOPE_IDENTITY())";

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
                iop.Add("@ModifiedDate", DateTime.UtcNow);
                iop.Add("@ModifiedBy", userId);
                iop.Add("@CreatedBy", userId);
                iop.Add("@CreatedDate", DateTime.UtcNow);
                iop.Add("@IsDeleted", false);

                insertedOrderId = await conn.ExecuteScalarAsync<int>(insertOrderSql, iop, t);

                foreach (var item in request.OrderItems)
                {
                    item.OrderId = insertedOrderId;
                    item.CreatedDate = DateTime.UtcNow;
                    item.CreatedBy = userId;
                    item.ModifiedDate = DateTime.UtcNow;
                    item.ModifiedBy = userId;
                }

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, (SqlTransaction)t))
                {
                    bulkCopy.DestinationTableName = "OrderItems";

                    DataTable dt = DataTableConverter.OrderItemsToDataTable(request.OrderItems);

                    await bulkCopy.WriteToServerAsync(dt);
                }

                await t.CommitAsync();
            }
            catch (Exception e)
            {
                await t.RollbackAsync();
                return 0;
            }
            finally { await conn.CloseAsync(); }

            return insertedOrderId;
        }

        public async Task<bool> DeleteSingleOrder(int request, int userId)
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


            string deleteOrderItemMultipleSql = @"  update o set
	                                                        o.ModifiedBy = @UserId,
	                                                        o.ModifiedDate = GETDATE(),
	                                                        o.IsDeleted = 1
                                                        from OrderItems o
                                                        where o.OrderId = @OrderId and o.Id in ";

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
                #region Deleting order items in bulk
                string listOfIdsToDelete = "(" + string.Join(",", deleteList.Select(x => x.Id)) + ")";

                deleteOrderItemMultipleSql = deleteOrderItemMultipleSql + listOfIdsToDelete;

                var deleteParams = new DynamicParameters();
                deleteParams.Add("@UserId", userId);
                deleteParams.Add("@OrderId", request.Id);

                await conn.ExecuteAsync(deleteOrderItemMultipleSql, deleteParams, transaction);
                #endregion

                //AddList Processing
                #region Bulk Insert all order items to add
                if (addList.Count > 0)
                {
                    foreach (var item in addList)
                    {
                        item.CreatedDate = DateTime.UtcNow;
                        item.CreatedBy = userId;
                        item.ModifiedDate = DateTime.UtcNow;
                        item.ModifiedBy = userId;
                    }

                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, (SqlTransaction)transaction))
                    {
                        bulkCopy.DestinationTableName = "OrderItems";

                        DataTable dt = DataTableConverter.OrderItemsToDataTable(addList);

                        await bulkCopy.WriteToServerAsync(dt);
                    }
                }
                #endregion

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

        public async Task<PagedOrderListModel> GetAllOrdersWithPagination(DateTime startDate, DateTime endDate, int pageSize, int pageNumber, int userId)
        {
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(); 

                // Common parameters
                var parameters = new DynamicParameters();

                parameters.Add("@StartDate", startDate);
                parameters.Add("@EndDate", endDate);

                parameters.Add("@PageNumber", pageNumber);
                parameters.Add("@PageSize", pageSize);

                parameters.Add("@UserId", userId);

                const string SQL_SERVER_QUERY = @"
                            DECLARE @TotalOrders INT, @TotalPages INT;

                            drop table if exists #OrderIds
                            create table #OrderIds(OrderId int)

                            insert into #OrderIds(OrderId)
                            SELECT o.Id
                            FROM Orders o
                            WHERE o.OrderDate >= CONVERT(DATE, @StartDate) 
                            AND o.OrderDate <= CONVERT(DATE, @EndDate)
                            AND o.IsDeleted = 0
                            AND (@UserId = 0 OR o.UserId = @UserId)
                            ORDER BY o.OrderDate DESC
                            OFFSET (@PageNumber - 1) * @PageSize ROWS
                            FETCH NEXT @PageSize ROWS ONLY;

                            select 
                                *
                            from Orders
                            where Id in (select OrderId from #OrderIds)

                            select oi.*
                            from OrderItems oi
                            where oi.OrderId in (select OrderId from #OrderIds)

                            SELECT @TotalOrders = COUNT(*)
                            FROM Orders
                            WHERE OrderDate >= CONVERT(DATE, @StartDate) 
                            AND OrderDate <= CONVERT(DATE, @EndDate);

                            SELECT @TotalPages = CEILING(CAST(@TotalOrders AS FLOAT) / @PageSize);

                            SELECT @TotalPages AS TotalPages;
                            SELECT @TotalOrders AS TotalOrders;

                            drop table if exists #OrderIds
                        ";

                var reader = await conn.QueryMultipleAsync(SQL_SERVER_QUERY, parameters);

                List<OrderModel> orders = reader.Read<OrderModel>().ToList();
                List<OrderItemModel> itemList = reader.Read<OrderItemModel>().ToList();

                foreach(var order in orders)
                {
                    order.OrderItems = itemList.Where(x=>x.OrderId == order.Id).ToList();
                }

                int totalPages = reader.ReadSingle<int>();
                int totalOrders = reader.ReadSingle<int>();

                return new PagedOrderListModel()
                {
                    TotalOrders = totalOrders,
                    TotalPages = totalPages,
                    OrderList = orders
                };
            }
            catch (Exception e)
            {
                // Optionally log e.Message
                return null;
            }
        }

        public async Task<OrderModel> GetOrderById(int orderId)
        {
            const string fetchOrderSql = @"select * from Orders where Id = @OrderId and IsDeleted = 0;
                                            select * from OrderItems where OrderId = @OrderId and IsDeleted = 0;";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(); 

                DynamicParameters parameters = new DynamicParameters();
                parameters.Add("@OrderId", orderId);

                GridReader reader = await conn.QueryMultipleAsync(fetchOrderSql, parameters);

                OrderModel model = reader.Read<OrderModel>().First();

                model.OrderItems = reader.Read<OrderItemModel>().ToList();

                return model;
            }
            catch (SqlException se)
            {
                return null;
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
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(); 

                await conn.OpenAsync();

                return (List<OrderItemModel>)await conn.QueryAsync<OrderItemModel>(sql, dynamicParameters);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<ConsumerResponseModel> InsertOrderCreateEvent(int orderId)
        {
            const string sql = @"INSERT INTO OrderEventLogs (OrderId, CreatedAt) VALUES (@OrderId, @CreatedAt);";

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@OrderId", orderId);
                parameters.Add("@CreatedAt", DateTime.UtcNow);

                try
                {
                    await conn.ExecuteAsync(sql, parameters);

                    return new ConsumerResponseModel
                    {
                        Status = true,
                        Message = "Order inserted successfully"
                    };
                }
                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    // UNIQUE constraint violation → duplicate Kafka message
                    return new ConsumerResponseModel
                    {
                        Status = true,
                        Message = "Order already exists (idempotent)"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ConsumerResponseModel
                {
                    Status = false,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace ?? "Stack trace unavailable"
                };
            }
        }

        public async Task<int> GetOrderCount()
        {
            string sql = @"select Count(*) from Orders";
            try
            {
                await using var conn = new SqlConnection(_connectionString);
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
