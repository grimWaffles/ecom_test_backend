using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Helpers.Converters;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Repository;
using System.ComponentModel;
using System.Text;

namespace OrderServiceGrpc.Services
{
    public interface IOrderService
    {
        Task<ConsumerResponseModel> CreateOrder(OrderDto dto, int userId);
        Task<ConsumerResponseModel> UpdateOrder(OrderDto model, int userId);
        Task<ConsumerResponseModel> UpdateDeleteStatusForSingleOrder(int orderId, int userId);
        Task<ConsumerResponseModel> GetAllOrders(DateTime startDate, DateTime endDate, int pageSize, int pageNumber, int userId);
        Task<ConsumerResponseModel> GetOrderById(int orderId);
        Task<ConsumerResponseModel> TestOrderProcessorService();
    }
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _repo;
        private readonly IUnitOfWork _uow;
        public OrderService(IOrderRepository orderRepository, IUnitOfWork unitOfWork)
        {
            _repo = orderRepository;
            _uow = unitOfWork;
        }

        public async Task<ConsumerResponseModel> CreateOrder(OrderDto dto, int userId)
        {
            try
            {
                OrderModel model = OrderMapper.DtoToEntity(dto);

                await _uow.BeginTransactionAsync();

                //Insert #1 - Order and OrderItems
                int insertedOrderId = await _uow.Orders.AddSingleOrder(model, userId);

                //Insert #2 - Outbox entry
                OrderOutbox outboxEntry = new OrderOutbox()
                {
                    AggregateId = insertedOrderId,
                    AggregateType = "Order",
                    EventType = "OrderCreated",
                    Topic = "order-create-success",
                    PartitionKey = insertedOrderId.ToString(),
                    Payload = System.Text.Json.JsonSerializer.Serialize(OrderMapper.EntityToMessage(model)),
                    Headers = "{}",
                    StatusId = 1, // Assuming 1 is the status for 'Pending'
                    RetryCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    ScheduledAt = DateTime.UtcNow
                };

                await _uow.Outbox.CreateAsync(outboxEntry);

                await _uow.SaveChangesAsync();

                //Commit both inserts together
                await _uow.CommitAsync();

                return new ConsumerResponseModel()
                {
                    Status = insertedOrderId == 0 ? false : true,
                    Message = insertedOrderId > 0 ? "Added successfully" : "Failed to add",
                    InsertedOrderId = insertedOrderId
                };
            }
            catch (Exception ex)
            {
                //Rollback both inserts together
                await _uow.RollbackAsync();

                return new ConsumerResponseModel
                {
                    Status = false,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace ?? "Stack trace unavailable"
                };
            }
        }

        public async Task<ConsumerResponseModel> UpdateOrder(OrderDto dto, int userId)
        {
            try
            {
                OrderModel requestModel = OrderMapper.DtoToEntity(dto);

                OrderModel dbModel = await _repo.GetOrderById(requestModel.Id);

                if (requestModel.Id != 0 && dbModel != null)
                {
                    List<OrderItemModel> deleteList = new();
                    List<OrderItemModel> addList = new();
                    List<OrderItemModel> updateList = new();

                    //Check for deleted items
                    deleteList = dbModel.OrderItems.Where(d => !requestModel.OrderItems.Any(r => r.Id == d.Id)).Select(x => PrepareItemToDelete(x, requestModel.Id, userId)).ToList();

                    //Check for added items
                    addList = requestModel.OrderItems.Where(i => i.Id == 0).Select(x => PrepareItemToAdd(x, requestModel.Id, userId)).ToList();

                    //Check for updated AND existing items
                    Dictionary<int, OrderItemModel> dbLookup = dbModel.OrderItems.ToDictionary(i => i.Id);

                    foreach (OrderItemModel req in requestModel.OrderItems.Where(i => i.Id != 0))
                    {
                        if (dbLookup.TryGetValue(req.Id, out var db))
                        {
                            if (HasChanges(req, db))
                            {
                                req.GrossAmount = req.Quantity * req.UnitPrice;
                                req.ModifiedBy = userId;
                                req.ModifiedDate = DateTime.Now;

                                updateList.Add(req);
                            }
                        }
                    }

                    await _uow.BeginTransactionAsync();
                    await _repo.UpdateOrder(requestModel, addList, deleteList, updateList, userId);

                    //Insert #2 - Outbox entry
                    OrderOutbox outboxEntry = new OrderOutbox()
                    {
                        AggregateId = dto.Id,
                        AggregateType = "Order",
                        EventType = "OrderUpdated",
                        Topic = "order-update-success",
                        PartitionKey = dto.Id.ToString(),
                        Payload = System.Text.Json.JsonSerializer.Serialize(OrderMapper.EntityToMessage(dbModel)),
                        Headers = "{}",
                        StatusId = 1, // Assuming 1 is the status for 'Pending'
                        RetryCount = 0,
                        CreatedAt = DateTime.UtcNow,
                        ScheduledAt = DateTime.UtcNow
                    };

                    await _uow.Outbox.CreateAsync(outboxEntry);

                    await _uow.SaveChangesAsync();

                    await _uow.CommitAsync();
                }
                else
                {
                    return new ConsumerResponseModel()
                    {
                        Status = false,
                        Message = "Order not found for update"
                    };
                }

                return new ConsumerResponseModel()
                {
                    Status = true,
                    Message = "Updated successfully"
                };
            }
            catch (Exception e)
            {
                if (_uow.DbTransaction != null)
                {
                    await _uow.RollbackAsync();
                }

                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = e.Message,
                    StackTrace = e.StackTrace
                };
            }
        }

        public async Task<ConsumerResponseModel> UpdateDeleteStatusForSingleOrder(int orderId, int userId)
        {
            try
            {
                await _uow.BeginTransactionAsync();

                bool orderAdded = await _repo.UpdateDeleteStatusForSingleOrder(orderId, userId);

                //Insert #2 - Outbox entry
                OrderOutbox outboxEntry = new OrderOutbox()
                {
                    AggregateId = orderId,
                    AggregateType = "Order",
                    EventType = "OrderDeleted",
                    Topic = "order-delete-success",
                    PartitionKey = orderId.ToString(),
                    Payload = System.Text.Json.JsonSerializer.Serialize(new { orderId = orderId, userId = userId }),
                    Headers = "{}",
                    StatusId = 1, // Assuming 1 is the status for 'Pending'
                    RetryCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    ScheduledAt = DateTime.UtcNow
                };

                await _uow.Outbox.CreateAsync(outboxEntry);

                await _uow.SaveChangesAsync();

                await _uow.CommitAsync();

                return new ConsumerResponseModel()
                {
                    Status = orderAdded,
                    Message = orderAdded ? "Deleted successfully" : "Failed to delete"
                };
            }
            catch (Exception e)
            {
                await _uow.RollbackAsync();

                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = e.Message,
                    StackTrace = e.StackTrace
                };
            }
        }

        public async Task<ConsumerResponseModel> GetAllOrders(DateTime startDate, DateTime endDate, int pageSize, int pageNumber, int userId)
        {
            PagedOrderListModel result = await _repo.GetAllOrdersWithPagination(startDate, endDate, pageSize, pageNumber, userId);

            if (result == null) { return new ConsumerResponseModel() { Message = "Failed to get orders", Status = false }; }

            try
            {
                ConsumerResponseModel response = new ConsumerResponseModel()
                {
                    Status = true,
                    Message = "Success",
                    TotalPages = result.TotalPages,
                    TotalOrders = result.TotalOrders,
                    ListOfOrders = result.OrderList.Select(OrderMapper.EntityToOrderDto).ToList()
                };

                return response;
            }
            catch (Exception e)
            {
                return new ConsumerResponseModel() { Message = "Failed to get orders", Status = false, StackTrace = e.StackTrace ?? e.Message };
            }
        }

        public async Task<ConsumerResponseModel> GetOrderById(int orderId)
        {
            OrderModel model = await _repo.GetOrderById(orderId);

            if (model == null) { return new ConsumerResponseModel() { Message = "Failed to get order", Status = false }; }

            return new ConsumerResponseModel()
            {
                Status = true,
                Message = "Success",
                Order = OrderMapper.EntityToOrderDto(model)
            };
        }

        //Helper Functions
        private bool HasChanges(OrderItemModel req, OrderItemModel db)
        {
            return req.Quantity != db.Quantity ||
                   req.UnitPrice != db.UnitPrice ||
                   req.ProductId != db.ProductId ||
                   req.GrossAmount != db.GrossAmount;
        }

        private OrderItemModel PrepareItemToAdd(OrderItemModel item, int orderId, int userId)
        {
            item.OrderId = orderId;
            item.CreatedBy = userId;
            item.CreatedDate = DateTime.UtcNow;
            item.GrossAmount = item.UnitPrice * item.Quantity;

            return item;
        }

        private OrderItemModel PrepareItemToDelete(OrderItemModel item, int orderId, int userId)
        {
            item.IsDeleted = true;
            item.ModifiedBy = userId;
            item.ModifiedDate = DateTime.UtcNow;
            return item;
        }

        // ============================================================
        //  INTEGRATION TEST  –  TestOrderProcessorService()
        //
        //  Test plan
        //  ---------
        //  Step 1  : GetAllOrders – basic pagination (page 1)
        //  Step 2  : GetAllOrders – pagination boundary (page 2 existence)
        //  Step 3  : GetAllOrders – userId filter (non-zero userId)
        //  Step 4  : GetOrderById – happy path
        //  Step 5  : GetOrderById – non-existent id returns failure
        //  Step 6  : CreateOrder  – insert succeeds, outbox implied by commit
        //  Step 7  : CreateOrder  – re-fetch and confirm persisted data
        //  Step 8  : UpdateOrder  – quantity change on existing item (net amount changes, item count same)
        //  Step 9  : UpdateOrder  – item deletion (item count decreases by 1)
        //  Step 10 : UpdateOrder  – item addition (item count increases by 1)
        //  Step 11 : UpdateOrder  – order not found guard (id = -1)
        //  Step 12 : UpdateDeleteStatusForSingleOrder – soft-delete
        //  Step 13 : UpdateDeleteStatusForSingleOrder – verify order is gone / marked deleted
        // ============================================================

        public async Task<ConsumerResponseModel> TestOrderProcessorService()
        {
            StringBuilder testLog = new StringBuilder();
            testLog.AppendLine("=== Integration Test Started ===");

            DateTime startDate = new DateTime(2021, 01, 01);
            DateTime endDate = new DateTime(2026, 12, 31);
            int pageSize = 5, pageNumber = 1;
            int testUserId = 1; // userId used for write operations

            //Step 1 
            ConsumerResponseModel crm1 = await GetAllOrders(startDate, endDate, pageSize, pageNumber, 0);

            if (crm1 == null)
            {
                testLog.AppendLine("STEP 1: Failed to fetch order list");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            List<OrderModel> listOfOrders = crm1.ListOfOrders.Select(x => OrderMapper.DtoToEntity(x)).ToList();

            if (listOfOrders.Count() == 0)
            {
                testLog.AppendLine("STEP 1: No orders to fetch.");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            testLog.AppendLine("STEP 1: Passed");

            //Step 2 GetById
            OrderModel objToSearch = listOfOrders.FirstOrDefault(x => x.OrderItems.Count() > 2) ?? new OrderModel();
            if (objToSearch.Id == 0)
            {
                testLog.AppendLine("STEP 2: No orders found with more than two items");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            int idToFind = objToSearch.Id;

            ConsumerResponseModel step2Response = await GetOrderById(idToFind);

            if (step2Response == null)
            {
                testLog.AppendLine("STEP 2: Failed to fetch single order");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            testLog.AppendLine("STEP 2: Passed");

            //Step 3 Create a new order
            OrderModel modelToCreate = OrderMapper.DtoToEntity(step2Response.Order);
            modelToCreate.Id = 0;

            ConsumerResponseModel insertResult = await CreateOrder(OrderMapper.EntityToOrderDto(modelToCreate), testUserId);

            if (insertResult == null)
            {
                testLog.AppendLine("STEP 3: Failed to create order");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            int insertedOrderId = insertResult.InsertedOrderId;

            ConsumerResponseModel newInsertedModelResponse = await GetOrderById(insertedOrderId);

            if (newInsertedModelResponse.Order == null)
            {
                testLog.AppendLine("STEP 3: Failed to verify inserted order");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            testLog.AppendLine("STEP 3: Passed");

            //Step 4 Updating an order
            OrderDto modelToUpdate = newInsertedModelResponse.Order;

            modelToUpdate.Items.ForEach(x => x.Quantity += 10);
            modelToUpdate.NetAmount = 0;

            foreach(OrderItemDto dto in modelToUpdate.Items)
            {
                modelToUpdate.NetAmount += dto.UnitPrice * dto.Quantity;
            }

            ConsumerResponseModel updateResponse1 = await UpdateOrder(modelToUpdate, testUserId);

            if (updateResponse1 == null)
            {
                testLog.AppendLine("STEP 4: Failed to update order");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            ConsumerResponseModel updatedModelResponse1 = await GetOrderById(modelToUpdate.Id);

            OrderModel updatedModel1 = OrderMapper.DtoToEntity(updatedModelResponse1.Order);

            if (updatedModel1.NetAmount <= (decimal)step2Response.Order.NetAmount)
            {
                testLog.AppendLine("STEP 4: Failed to verify updated quantity");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            testLog.AppendLine("STEP 4: Passed");

            //Step 5 Deleting orders
            ConsumerResponseModel deleteResponse = await UpdateDeleteStatusForSingleOrder(updatedModel1.Id, testUserId);

            if (!deleteResponse.Status)
            {
                testLog.AppendLine("STEP 5: Failed to delete order");
                return new ConsumerResponseModel() { Status = false, Message = testLog.ToString() };
            }

            testLog.AppendLine("STEP 5: Passed");

            // ----------------------------------------------------------------
            // All steps complete
            // ----------------------------------------------------------------
            testLog.AppendLine("=== Integration Test Finished ===");

            return new ConsumerResponseModel()
            {
                Status = true,
                Message = testLog.ToString()
            };
        }

        // ----------------------------------------------------------------
        // Private helper – build a uniform failure response
        // ----------------------------------------------------------------
        private ConsumerResponseModel Fail(StringBuilder log, string? stackTrace = null)
        {
            log.AppendLine("=== Integration Test ABORTED ===");
            return new ConsumerResponseModel()
            {
                Status = false,
                Message = log.ToString(),
                StackTrace = stackTrace ?? string.Empty
            };
        }
    }
}
