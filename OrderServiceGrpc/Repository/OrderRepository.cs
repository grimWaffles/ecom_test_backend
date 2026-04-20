using Azure.Core;
using Dapper;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Helpers.Converters;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.ConfigModels;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Transactions;
using static Dapper.SqlMapper;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace OrderServiceGrpc.Repository
{
    public interface IOrderRepository
    {
        Task<OrderModel> GetOrderById(int orderId);
        Task<int> AddSingleOrder(OrderModel request, int userId);
        Task<bool> UpdateOrder(OrderModel request, List<OrderItemModel> addList, List<OrderItemModel> deleteList, List<OrderItemModel> updateList, int userId);
        Task<bool> UpdateDeleteStatusForSingleOrder(int requestId, int userId);
        Task<int> GetOrderCount();
        Task<PagedOrderListModel> GetAllOrdersWithPagination(DateTime startDate, DateTime endDate, int pageSize, int pageNumber, int userId);
        Task<List<OrderItemModel>> GetOrderItemsForOrder(int orderId);
    }

    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _appDbContext;
        private readonly string _connectionString;
        private readonly ILogger<OrderRepository> _logger;
        private readonly UnitOfWorkContext _uowContext;

        public OrderRepository(AppDbContext appDbContext, IOptions<DatabaseConfig> dbConfig, UnitOfWorkContext unitOfWorkContext, IOptions<DatabaseConnection> connectionStrings, ILogger<OrderRepository> logger)
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
            _uowContext = unitOfWorkContext;
            _appDbContext = appDbContext;
        }

        private async Task<(IDbConnection Connection, IDbTransaction? Transaction, bool OwnConnection)> GetConnectionAsync()
        {
            if (_uowContext.IsUnderUnitOfWork)
            {
                if (_appDbContext.Database.GetDbConnection().State != ConnectionState.Open)
                    await _appDbContext.Database.OpenConnectionAsync();
                return (_appDbContext.Database.GetDbConnection(), _appDbContext.Database.CurrentTransaction?.GetDbTransaction(), false);
            }

            SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return (connection, null, true);
        }

        public async Task<int> AddSingleOrder(OrderModel request, int userId)
        {
            int insertedOrderId = 0;
            const string insertOrderSql = @" INSERT INTO [dbo].[Orders]
                                   ([OrderDate] ,[OrderCounter] ,[UserId]  ,[Status] ,[NetAmount] ,[CreatedBy] ,[CreatedDate] ,[IsDeleted],[ModifiedDate],[ModifiedBy])
                             VALUES (@OrderDate,  @OrderCounter, @UserId, @Status, @NetAmount, @CreatedBy,  @CreatedDate, @IsDeleted, @ModifiedDate,@ModifiedBy ); select Convert(int,SCOPE_IDENTITY())";

            (IDbConnection? sqlConnection, IDbTransaction? dbTransaction, bool ownConnection) = await GetConnectionAsync();

            SqlConnection conn = (SqlConnection)sqlConnection;
            DbTransaction t = (DbTransaction)(dbTransaction ?? await conn.BeginTransactionAsync());

            _logger.LogInformation("AddSingleOrder: starting insert for user {UserId}, items={ItemCount}", request.UserId, request.OrderItems?.Count ?? 0);

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

                if(ownConnection)
                    await t.CommitAsync();

                _logger.LogInformation("AddSingleOrder: committed order {OrderId} for user {UserId}", insertedOrderId, request.UserId);
            }
            catch (Exception e)
            {
                try { await t.RollbackAsync(); } catch { /* ignore rollback exception */ }
                _logger.LogError(e, "AddSingleOrder: failed insert for user {UserId}", request.UserId);
                throw;
            }
            finally
            {
                if(ownConnection)
                    await conn.DisposeAsync();
            }

            return insertedOrderId;
        }

        public async Task<bool> UpdateDeleteStatusForSingleOrder(int request, int userId)
        {
            const string sql = @"   update OrderItems set IsDeleted = case when IsDeleted = 1 then 0 when isDeleted = 0 then 1 end, ModifiedBy = @ModifiedBy, ModifiedDate = @ModifiedDate where OrderId = @OrderId;
                                        update Orders set IsDeleted = case when IsDeleted = 1 then 0 when isDeleted = 0 then 1 end, ModifiedBy = @ModifiedBy, ModifiedDate = @ModifiedDate where Id = @OrderId";

            DynamicParameters parameters = new DynamicParameters();

            parameters.Add("@OrderId", request);
            parameters.Add("@ModifiedBy", userId);
            parameters.Add("@ModifiedDate", DateTime.UtcNow);

            //await using SqlConnection conn = new SqlConnection(_connectionString);
            //await conn.OpenAsync();
            //await using DbTransaction transaction = await conn.BeginTransactionAsync();

            var (sqlConnection, dbTransaction, ownConnection) = await GetConnectionAsync();

            await using SqlConnection conn = (SqlConnection)sqlConnection;
            await using DbTransaction transaction = (DbTransaction)(dbTransaction ?? await conn.BeginTransactionAsync());

            _logger.LogInformation("UpdateDeleteStatusForSingleOrder: toggling delete status for OrderId {OrderId} by user {UserId}", request, userId);

            try
            {
                await conn.ExecuteAsync(sql, parameters, transaction);

                if (ownConnection)
                    await transaction.CommitAsync();

                _logger.LogInformation("UpdateDeleteStatusForSingleOrder: committed toggle for OrderId {OrderId}", request);
                return true;
            }
            catch (Exception e)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore rollback */ }
                _logger.LogError(e, "UpdateDeleteStatusForSingleOrder: failed for OrderId {OrderId}", request);
                return false;
            }
            finally
            {
                if (ownConnection)
                    await conn.DisposeAsync();
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

            //await using var conn = new SqlConnection(_connectionString);
            //await conn.OpenAsync();
            //await using var transaction = await conn.BeginTransactionAsync();

            var (sqlConnection, dbTransaction, ownConnection) = await GetConnectionAsync();

            SqlConnection conn = (SqlConnection)sqlConnection;
            DbTransaction transaction = (DbTransaction)(dbTransaction ?? await conn.BeginTransactionAsync());

            _logger.LogInformation("UpdateOrder: starting update for OrderId {OrderId} by user {UserId}", request.Id, userId);

            try
            {
                //Delete List Processing
                #region Deleting order items in bulk
                string listOfIdsToDelete = "(" + string.Join(",", deleteList.Select(x => x.Id)) + ")";

                deleteOrderItemMultipleSql = deleteOrderItemMultipleSql + listOfIdsToDelete;

                var deleteParams = new DynamicParameters();
                deleteParams.Add("@UserId", userId);
                deleteParams.Add("@OrderId", request.Id);

                int deleteRows = await conn.ExecuteAsync(deleteOrderItemMultipleSql, deleteParams, transaction);
                _logger.LogInformation("UpdateOrder: deleted {Count} order items for OrderId {OrderId}", deleteRows, request.Id);
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

                    _logger.LogInformation("UpdateOrder: inserted {Count} new order items for OrderId {OrderId}", addList.Count, request.Id);
                }
                #endregion

                //UpdateList processing
                int updateCount = 0;
                foreach (var item in updateList)
                {
                    var updateParams = new DynamicParameters();
                    updateParams.Add("@Id", item.Id);
                    updateParams.Add("@Quantity", item.Quantity);
                    updateParams.Add("@GrossAmount", item.GrossAmount);
                    updateParams.Add("@UserId", userId);
                    updateParams.Add("@OrderId", item.OrderId);

                    updateCount += await conn.ExecuteAsync(updateOrderItemSingleSql, updateParams, transaction);
                }
                _logger.LogInformation("UpdateOrder: updated {Count} order items for OrderId {OrderId}", updateCount, request.Id);

                //Main order object
                var orderParams = new DynamicParameters();

                orderParams.Add("@OrderDate", request.OrderDate);
                orderParams.Add("@UserId", request.UserId);
                orderParams.Add("@Status", request.Status);
                orderParams.Add("@NetAmount", request.NetAmount);
                orderParams.Add("@ModifiedBy", userId);
                orderParams.Add("@ModifiedDate", DateTime.UtcNow);
                orderParams.Add("@Id", request.Id);

                int orderRows = await conn.ExecuteAsync(updateOrderObjectSqlSingle, orderParams, transaction);
                _logger.LogInformation("UpdateOrder: updated main order object rows={Rows} for OrderId {OrderId}", orderRows, request.Id);

                //Commit the transaction
                if (ownConnection)
                    await transaction.CommitAsync();

                _logger.LogInformation("UpdateOrder: committed transaction for OrderId {OrderId}", request.Id);

                return true;
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore rollback failure */ }
                _logger.LogError(ex, "UpdateOrder: failed for OrderId {OrderId}", request.Id);
                return false;
            }
            finally
            {
                if (ownConnection)
                    await conn.DisposeAsync();
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

                foreach (var order in orders)
                {
                    order.OrderItems = itemList.Where(x => x.OrderId == order.Id).ToList();
                }

                int totalPages = reader.ReadSingle<int>();
                int totalOrders = reader.ReadSingle<int>();

                _logger.LogInformation("GetAllOrdersWithPagination: returned {Orders} orders across {Pages} pages for date range {Start}-{End}", orders.Count, totalPages, startDate, endDate);

                return new PagedOrderListModel()
                {
                    TotalOrders = totalOrders,
                    TotalPages = totalPages,
                    OrderList = orders
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetAllOrdersWithPagination: failed between {StartDate} and {EndDate}", startDate, endDate);
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
                
                if(conn.State == ConnectionState.Closed)
                {
                    await conn.OpenAsync();
                }

                DynamicParameters parameters = new DynamicParameters();
                parameters.Add("@OrderId", orderId);

                GridReader reader = await conn.QueryMultipleAsync(fetchOrderSql, parameters);

                OrderModel model = reader.Read<OrderModel>().FirstOrDefault();

                if (model == null)
                {
                    _logger.LogWarning("GetOrderById: no order found for OrderId {OrderId}", orderId);
                    return null;
                }

                model.OrderItems = reader.Read<OrderItemModel>().ToList();

                _logger.LogInformation("GetOrderById: fetched order {OrderId} with {ItemCount} items", orderId, model.OrderItems?.Count ?? 0);

                return model;
            }
            catch (SqlException se)
            {
                _logger.LogError(se, "GetOrderById: SQL failure for OrderId {OrderId}", orderId);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetOrderById: failure for OrderId {OrderId}", orderId);
                return null;
            }
        }

        public async Task<List<OrderItemModel>> GetOrderItemsForOrder(int orderId)
        {
            string sql = @"select * from OrderItems where OrderId = @OrderId and IsDeleted = 0";

            DynamicParameters dynamicParameters = new DynamicParameters();
            dynamicParameters.Add("@OrderId", orderId);
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var items = (await conn.QueryAsync<OrderItemModel>(sql, dynamicParameters)).ToList();

                _logger.LogInformation("GetOrderItemsForOrder: returned {Count} items for OrderId {OrderId}", items.Count, orderId);

                return items;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetOrderItemsForOrder: failed for OrderId {OrderId}", orderId);
                return null;
            }
        }

        public async Task<int> GetOrderCount()
        {
            string sql = @"select Count(*) from Orders";
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                int count = await conn.ExecuteScalarAsync<int>(sql);
                _logger.LogInformation("GetOrderCount: count={Count}", count);

                return count;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetOrderCount: failed");
                return 0;
            }
        }
    }
}
