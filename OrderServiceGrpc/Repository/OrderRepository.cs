using Azure.Core;
using Dapper;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
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
        Task<bool> AddOrder(OrderModel request, int userId);
        Task<bool> UpdateOrder(OrderModel request, List<OrderItemModel> addList, List<OrderItemModel> deleteList, List<OrderItemModel> updateList, int userId);
        Task<bool> DeleteOrder(int requestId, int userId);
        Task<int> GetOrderCount();
        Task<Tuple<int, int, List<OrderModel>>> GetAllOrdersWithPagination(DateTime startDate, DateTime endDate, int pageSize, int pageNumber);
        Task<List<OrderItemModel>> GetOrderItemsForOrder(int orderId);
        Task<ProcessorResponseModel> InsertOrderCreateEvent(int orderId);
    }

    public class OrderRepository : IOrderRepository
    {
        private readonly string _connectionString;
        private readonly string _dbType;

        public OrderRepository(IOptions<DatabaseConfig> dbConfig, IOptions<DatabaseConnection> connectionStrings)
        {
            _connectionString = (dbConfig.Value.Database.ToLower(), dbConfig.Value.Mode.ToLower()) switch
            {
                ("mysql", "local") => connectionStrings.Value.MySqlConnection,
                ("mysql", "docker") => connectionStrings.Value.MySqlDockerConnection,
                ("sqlserver", "local") => connectionStrings.Value.SqlServerConnection,
                ("sqlserver", "docker") => connectionStrings.Value.SqlServerDockerConnection,
                _ => ""
            };
            _dbType = dbConfig.Value.Database;
        }

        private DbConnection GetDatabaseConnection()
        {
            if (_dbType == "mysql")
                return new MySqlConnection(_connectionString);

            return new SqlConnection(_connectionString);
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
            finally { await conn.CloseAsync(); }

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
                if (addList.Count > 0)
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

        public async Task<Tuple<int, int, List<OrderModel>>> GetAllOrdersWithPagination(DateTime startDate, DateTime endDate, int pageSize, int pageNumber)
        {
            using var conn = GetDatabaseConnection();
            try
            {
                if (_dbType == "sqlserver")
                {
                    return await AllOrdersSqlServerPaginationMode(conn, startDate, endDate, pageSize, pageNumber);
                }
                else
                {
                    return await AllOrdersMySqlPaginationMode(conn, startDate, endDate, pageSize, pageNumber);
                }
            }
            catch (Exception e)
            {
                // Optionally log e.Message
                return null;
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        private async Task<Tuple<int, int, List<OrderModel>>> AllOrdersMySqlPaginationMode(DbConnection conn, DateTime startDate, DateTime endDate, int pageSize, int pageNumber)
        {
            // Common parameters
            var parameters = new DynamicParameters();
            parameters.Add("@StartDate", startDate.Date);
            parameters.Add("@EndDate", endDate.Date);
            parameters.Add("@PageSize", pageSize);
            parameters.Add("@Offset", (pageNumber - 1) * pageSize);
            try
            {
                const string MY_SQL_QUERY = @"select 
                            Id, OrderDate, OrderCounter, UserId, Status	, NetAmount, CreatedBy, CreatedDate, IsDeleted, ModifiedBy, ModifiedDate
                        from Orders 
                        
                        order by 
                            OrderDate desc, OrderCounter desc
                        limit @Offset, @PageSize;
                    ";

                const string MY_SQL_COUNT = @"
                        SELECT COUNT(*) 
                        FROM Orders o
                    ";

                await conn.OpenAsync();

                List<OrderModel> orderList = (await conn.QueryAsync<OrderModel>(MY_SQL_QUERY, parameters)).ToList();
                int totalOrders = Convert.ToInt32(await conn.ExecuteScalarAsync<long>(MY_SQL_COUNT, parameters));
                int totalPages = Convert.ToInt32(System.Math.Ceiling((decimal)totalOrders / pageSize));

                return Tuple.Create(totalPages, totalOrders, orderList);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private async Task<Tuple<int, int, List<OrderModel>>> AllOrdersSqlServerPaginationMode(DbConnection conn, DateTime startDate, DateTime endDate, int pageSize, int pageNumber)
        {

            // Common parameters
            var parameters = new DynamicParameters();

            parameters.Add("@StartDate", startDate);
            parameters.Add("@EndDate", endDate);

            parameters.Add("@PageNumber", pageNumber);
            parameters.Add("@PageSize", pageSize);

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

            await conn.OpenAsync();
            var reader = await conn.QueryMultipleAsync(SQL_SERVER_QUERY, parameters);

            List<OrderModel> orders = reader.Read<OrderModel>().ToList();
            List<OrderItemModel> itemList = reader.Read<OrderItemModel>().ToList();

            foreach(var order in orders)
            {
                order.OrderItems = itemList.Where(x=>x.OrderId == order.Id).ToList();
            }

            int totalPages = reader.ReadSingle<int>();
            int totalOrders = reader.ReadSingle<int>();

            return Tuple.Create(totalPages, totalOrders, orders);
        }

        public async Task<OrderModel> GetOrderById(int orderId)
        {
            const string fetchOrderSql = @"select * from Orders where Id = @OrderId and IsDeleted = 0;
                                            select * from OrderItems where OrderId = @OrderId and IsDeleted = 0;";

            using var conn = GetDatabaseConnection();

            try
            {
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
            finally { await conn.CloseAsync(); }
        }

        public async Task<List<OrderItemModel>> GetOrderItemsForOrder(int orderId)
        {
            string sql = @"select * from OrderItems where OrderId = @OrderId and IsDeleted = 0";
            DynamicParameters dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@Id", orderId);
            try
            {
                using (var conn = GetDatabaseConnection())
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

        public async Task<ProcessorResponseModel> InsertOrderCreateEvent(int orderId)
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

                    return new ProcessorResponseModel
                    {
                        Status = true,
                        Message = "Order inserted successfully"
                    };
                }
                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    // UNIQUE constraint violation → duplicate Kafka message
                    return new ProcessorResponseModel
                    {
                        Status = true,
                        Message = "Order already exists (idempotent)"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ProcessorResponseModel
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
                using var conn = GetDatabaseConnection();
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
