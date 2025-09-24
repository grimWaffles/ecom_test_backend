using Azure.Core;
using Dapper;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using OrderServiceGrpc.Helpers.cs;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Protos;
using System.Data.Common;
using System.Diagnostics;
using System.Transactions;
using Z.Dapper.Plus;
using static Dapper.SqlMapper;

namespace OrderServiceGrpc.Repository
{
    public interface IOrderRepository
    {
        #region Order
        Task<OrderModel> GetOrderById(OrderIdRequest request);
        Task<List<OrderModel>> GetAllOrders(OrderListRequest request);
        Task<bool> AddOrder(OrderModel request, int userId);
        Task<bool> UpdateOrder(OrderModel request, int userId);
        Task<bool> DeleteOrder(int requestId, int userId);
        Task<int> GetOrderCount();
        Task<Tuple<int, int, List<OrderModel>>> GetAllOrdersWithPagination(OrderListRequest request);
        #endregion

        #region OrderItems
        Task<List<OrderItemModel>> GetOrderItemsForOrder(int orderId);
        #endregion

        Task<bool> InsertOrderCreateEvent(int orderId);
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
            string insertOrderSql = @" INSERT INTO [dbo].[Orders]
                                   ([OrderDate] ,[OrderCounter] ,[UserId]  ,[Status] ,[NetAmount] ,[CreatedBy] ,[CreatedDate] ,[IsDeleted])
                             VALUES (@OrderDate,  @OrderCounter, @UserId, @Status, @NetAmount, @CreatedBy,  @CreatedDate, @IsDeleted ); select Convert(int,SCOPE_IDENTITY())";

            string insertOrderItemsSql = @" Insert into OrderItems(OrderId, ProductId, Quantity, GrossAmount, Status, CreatedBy, CreatedDate, IsDeleted)
                                            values (@OrderId, @ProductId, @Quantity, @GrossAmount, @Status, @CreatedBy, @CreatedDate, @IsDeleted)";

            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                await using DbTransaction t = await conn.BeginTransactionAsync();

                try
                {

                    DynamicParameters iop = new DynamicParameters();

                    iop.Add("@OrderDate", request.OrderDate);
                    iop.Add("@UserId", request.UserId);
                    iop.Add("@Status", request.Status);
                    iop.Add("@NetAmount", request.NetAmount);
                    iop.Add("@CreatedBy", request.CreatedBy);
                    iop.Add("@CreatedDate", request.CreatedDate);
                    iop.Add("IsDeleted", request.IsDeleted);

                    int insertedOrderId = await conn.ExecuteScalarAsync<int>(insertOrderSql, iop, t);

                    foreach (var item in request.OrderItems)
                    {
                        DynamicParameters parameters = new DynamicParameters();

                        parameters.Add("@OrderId", insertedOrderId);
                        parameters.Add("@ProductId", item.ProductId);
                        parameters.Add("@Quantity", item.Quatity);
                        parameters.Add("@GrossAmount", item.GrossAmount);
                        parameters.Add("@Status", item.Status);
                        parameters.Add("@CreatedBy", userId);
                        parameters.Add("@CreatedDate", DateTime.Now);
                        parameters.Add("@IsDeleted", false);

                        await conn.ExecuteAsync(insertOrderItemsSql, parameters, t);
                    }

                    await t.CommitAsync();
                }
                catch (Exception e)
                {
                    await t.RollbackAsync();
                }
                finally
                {
                    await t.DisposeAsync();
                }

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public async Task<bool> InsertOrderCreateEvent(int orderId)
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

                    await conn.ExecuteAsync(sql,iop);

                    return true;
                }

                catch(Exception e) { Console.WriteLine(e.StackTrace); return false; }

                finally { await conn.CloseAsync(); }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);

                return false; 
            }
        }

        public async Task<bool> DeleteOrder(int request, int userId)
        {
            const string deleteOrderItemsSql = @"update OrderItems set IsDeleted = 1, ModifiedBy = @ModifiedBy, ModifiedDate = @ModifiedDate where OrderId = @OrderId";
            const string deleteOrderSql = @"update Orders set IsDeleted = 1, ModifiedBy = @ModifiedBy, ModifiedDate = @ModifiedDate where Id = @OrderId";

            await using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            DynamicParameters parameters = new DynamicParameters();
            
            parameters.Add("@ModifiedBy", userId);
            parameters.Add("@ModifiedDate", DateTime.Now);

            await using DbTransaction transaction = await conn.BeginTransactionAsync();

            try
            {
                await conn.ExecuteAsync(deleteOrderItemsSql, parameters, transaction);
                await conn.ExecuteAsync(deleteOrderSql, parameters, transaction);

                await transaction.CommitAsync();
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                return false;
            }

            return true;
        }

        public async Task<bool> UpdateOrder(OrderModel request, int userId)
        {
            const string updateOrderSql = @"
                                            UPDATE Orders SET
                                                OrderDate = @OrderDate,
                                                UserId = @UserId,
                                                Status = @Status,
                                                NetAmount = @NetAmount,
                                                ModifiedBy = @ModifiedBy,
                                                ModifiedDate = @ModifiedDate,
                                                IsDeleted = @IsDeleted
                                            WHERE Id = @Id";

            const string deleteOrderItemsSql = @"update OrderItems set IsDeleted = 1, ModifiedBy = @ModifiedBy, ModifiedDate = @ModifiedDate where OrderId = @OrderId";
            const string insertOrderItemsSql = @" Insert into OrderItems(OrderId, ProductId, Quantity, GrossAmount, Status, CreatedBy, CreatedDate, IsDeleted)
                                            values (@OrderId, @ProductId, @Quantity, @GrossAmount, @Status, @CreatedBy, @CreatedDate, @IsDeleted)";
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                await using var transaction = await conn.BeginTransactionAsync();

                try
                {
                    var parameters = new DynamicParameters();
                    parameters.Add("OrderDate", request.OrderDate);
                    parameters.Add("UserId", request.UserId);
                    parameters.Add("Status", request.Status);
                    parameters.Add("NetAmount", request.NetAmount);
                    parameters.Add("ModifiedBy", userId);
                    parameters.Add("ModifiedDate", DateTime.UtcNow);
                    parameters.Add("IsDeleted", false);
                    parameters.Add("Id", request.Id);

                    await conn.ExecuteAsync(updateOrderSql, parameters, transaction);
                    await conn.ExecuteAsync(deleteOrderItemsSql, new { OrderId = request.Id }, transaction);

                    foreach (var item in request.OrderItems)
                    {
                        item.OrderId = request.Id;

                        var orderItemParams = new DynamicParameters();
                        orderItemParams.Add("OrderId", request.Id);
                        orderItemParams.Add("ProductId", item.ProductId);
                        orderItemParams.Add("Quantity", item.Quatity);
                        orderItemParams.Add("GrossAmount", item.GrossAmount);
                        orderItemParams.Add("Status", item.Status);
                        orderItemParams.Add("CreatedBy", userId);
                        orderItemParams.Add("CreatedDate", DateTime.Now);
                        orderItemParams.Add("IsDeleted", false);

                        await conn.ExecuteAsync(insertOrderItemsSql, item, transaction);
                    }

                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<OrderModel>> GetAllOrders(OrderListRequest request)
        {
            string sql = @"select * from Orders order by OrderDate desc";
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
                                    offset (@pageSize-1)*@PageNumber rows
                                    fetch next @PageSize rows only

                                    select @TotalOrders = count(*) from Orders where OrderDate >=  Convert(date,@StartDate) and OrderDate <= Convert(date,@EndDate)
                                    select @TotalPages = (@TotalOrders/@PageSize) + 1

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

                return Tuple.Create<int, int, List<OrderModel>>(totalPages,totalOrders, orders);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<OrderModel> GetOrderById(OrderIdRequest request)
        {
            const string fetchOrderSql = @"select * from Orders where Id = @OrderId;
                                            select * from OrderItems where OrderId = @OrderId and IsDeleted = 0;";

            try
            {
                await using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                DynamicParameters parameters = new DynamicParameters();
                parameters.Add("@OrderId", request.Id);

                GridReader reader = await conn.QueryMultipleAsync(fetchOrderSql, parameters);

                OrderModel model = reader.Read<OrderModel>().First();

                model.OrderItems = reader.Read<OrderItemModel>().ToList();

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
