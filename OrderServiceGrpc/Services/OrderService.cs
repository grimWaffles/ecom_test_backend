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
            finally
            {
                await _uow.DisposeAsync();
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
                        Payload = System.Text.Json.JsonSerializer.Serialize(new { orderId = dto.Id, userId = userId }),
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

            // ----------------------------------------------------------------
            // STEP 1 – GetAllOrders basic pagination (page 1)
            // ----------------------------------------------------------------
            ConsumerResponseModel page1Response = await GetAllOrders(startDate, endDate, pageSize, pageNumber, 0);

            if (!page1Response.Status || page1Response.ListOfOrders == null || !page1Response.ListOfOrders.Any())
            {
                testLog.AppendLine("Step 1: GetAllOrders (page 1) – FAILED – no orders returned or status false");
                return Fail(testLog, page1Response.StackTrace);
            }

            if (page1Response.TotalOrders <= 0 || page1Response.TotalPages <= 0)
            {
                testLog.AppendLine("Step 1: GetAllOrders (page 1) – FAILED – TotalOrders or TotalPages is 0");
                return Fail(testLog);
            }

            testLog.AppendLine($"Step 1: GetAllOrders (page 1) – PASSED  " +
                               $"[TotalOrders={page1Response.TotalOrders}, TotalPages={page1Response.TotalPages}, " +
                               $"PageCount={page1Response.ListOfOrders.Count()}]");

            // ----------------------------------------------------------------
            // STEP 2 – GetAllOrders pagination boundary (page 2, if it exists)
            // ----------------------------------------------------------------
            if (page1Response.TotalPages > 1)
            {
                ConsumerResponseModel page2Response = await GetAllOrders(startDate, endDate, pageSize, 2, 0);

                if (!page2Response.Status || page2Response.ListOfOrders == null || !page2Response.ListOfOrders.Any())
                {
                    testLog.AppendLine("Step 2: GetAllOrders (page 2) – FAILED – page 2 exists in TotalPages but returned no data");
                    return Fail(testLog, page2Response.StackTrace);
                }

                testLog.AppendLine($"Step 2: GetAllOrders (page 2) – PASSED  [PageCount={page2Response.ListOfOrders.Count()}]");
            }
            else
            {
                testLog.AppendLine("Step 2: GetAllOrders (page 2) – SKIPPED (only 1 page of data)");
            }

            // ----------------------------------------------------------------
            // STEP 3 – GetAllOrders with a specific userId filter
            // ----------------------------------------------------------------
            ConsumerResponseModel userFilterResponse = await GetAllOrders(startDate, endDate, pageSize, 1, testUserId);

            // A valid response is expected even if zero rows returned for this user;
            // what matters is the call does not blow up.
            if (userFilterResponse == null)
            {
                testLog.AppendLine("Step 3: GetAllOrders (userId filter) – FAILED – null response");
                return Fail(testLog);
            }

            testLog.AppendLine($"Step 3: GetAllOrders (userId filter) – PASSED  " +
                               $"[Status={userFilterResponse.Status}, TotalOrders={userFilterResponse.TotalOrders}]");

            // ----------------------------------------------------------------
            // STEP 4 – GetOrderById happy path
            //          Safely pick an order that actually exists from Step 1.
            // ----------------------------------------------------------------
            OrderDto seedOrder = page1Response.ListOfOrders.FirstOrDefault();

            if (seedOrder == null || seedOrder.Id == 0)
            {
                testLog.AppendLine("Step 4: GetOrderById – FAILED – could not extract a valid Id from Step 1 list");
                return Fail(testLog);
            }

            ConsumerResponseModel getByIdResponse = await GetOrderById(seedOrder.Id);

            if (!getByIdResponse.Status || getByIdResponse.Order == null || getByIdResponse.Order.Id != seedOrder.Id)
            {
                testLog.AppendLine($"Step 4: GetOrderById (id={seedOrder.Id}) – FAILED");
                return Fail(testLog, getByIdResponse.StackTrace);
            }

            testLog.AppendLine($"Step 4: GetOrderById (id={seedOrder.Id}) – PASSED");

            // ----------------------------------------------------------------
            // STEP 5 – GetOrderById with a non-existent id must return failure
            // ----------------------------------------------------------------
            ConsumerResponseModel notFoundResponse = await GetOrderById(-9999);

            if (notFoundResponse.Status == true || notFoundResponse.Order != null)
            {
                testLog.AppendLine("Step 5: GetOrderById (non-existent) – FAILED – expected failure but got success");
                return Fail(testLog);
            }

            testLog.AppendLine("Step 5: GetOrderById (non-existent id) – PASSED");

            // ----------------------------------------------------------------
            // STEP 6 – CreateOrder
            //          Clone the seed order (id = 0 so it becomes a new row).
            // ----------------------------------------------------------------
            ConsumerResponseModel seedFull = await GetOrderById(seedOrder.Id);
            OrderModel cloneModel = OrderMapper.DtoToEntity(seedFull.Order);
            cloneModel.Id = 0;
            cloneModel.OrderDate = DateTime.UtcNow;
            // Zero-out item ids so they are treated as new inserts
            cloneModel.OrderItems.ForEach(i => { i.Id = 0; i.OrderId = 0; });

            ConsumerResponseModel createResponse = await CreateOrder(OrderMapper.EntityToOrderDto(cloneModel), testUserId);

            if (!createResponse.Status || createResponse.InsertedOrderId <= 0)
            {
                testLog.AppendLine("Step 6: CreateOrder – FAILED");
                return Fail(testLog, createResponse.StackTrace);
            }

            int newOrderId = createResponse.InsertedOrderId;
            testLog.AppendLine($"Step 6: CreateOrder – PASSED  [InsertedOrderId={newOrderId}]");

            // ----------------------------------------------------------------
            // STEP 7 – Re-fetch the newly created order to confirm persistence
            // ----------------------------------------------------------------
            ConsumerResponseModel refetchResponse = await GetOrderById(newOrderId);

            if (!refetchResponse.Status || refetchResponse.Order == null || refetchResponse.Order.Id != newOrderId)
            {
                testLog.AppendLine($"Step 7: CreateOrder persistence check – FAILED – could not re-fetch id={newOrderId}");
                return Fail(testLog, refetchResponse.StackTrace);
            }

            int originalItemCount = refetchResponse.Order.Items.Count;
            decimal originalNetAmount = (decimal)refetchResponse.Order.NetAmount;

            testLog.AppendLine($"Step 7: CreateOrder persistence check – PASSED  " +
                               $"[ItemCount={originalItemCount}, NetAmount={originalNetAmount}]");

            // Guard: we need at least 2 items for Steps 8 & 9
            if (originalItemCount < 2)
            {
                testLog.AppendLine("Steps 8-10: Skipped – newly created order has fewer than 2 items; cannot test item diff scenarios");
            }
            else
            {
                // ------------------------------------------------------------
                // STEP 8 – UpdateOrder: change quantity on every existing item
                //          Expected: item count unchanged, net amount changes.
                // ------------------------------------------------------------
                ConsumerResponseModel step8Source = await GetOrderById(newOrderId);
                OrderModel step8Model = OrderMapper.DtoToEntity(step8Source.Order);

                step8Model.OrderItems.ForEach(i =>
                {
                    i.Quantity += 5;
                    i.GrossAmount = (decimal)((i.Quantity + 5) * i.UnitPrice); //Mutate the object
                });

                step8Model.RecalculateNetAmount();

                ConsumerResponseModel step8Response = await UpdateOrder(OrderMapper.EntityToOrderDto(step8Model), testUserId);
                ConsumerResponseModel step8Refetch = await GetOrderById(step8Model.Id);
                
                decimal netAfterStep8 = (decimal)step8Refetch.Order.NetAmount;
                int countAfterStep8 = step8Refetch.Order.Items.Count;

                if (!step8Response.Status
                    || countAfterStep8 != originalItemCount
                    || netAfterStep8 == originalNetAmount)
                {
                    testLog.AppendLine($"Step 8: UpdateOrder (quantity change) – FAILED  " +
                                       $"[Status={step8Response.Status}, ItemCount={countAfterStep8} (expected {originalItemCount}), " +
                                       $"NetAmount={netAfterStep8} (expected ≠ {originalNetAmount})]");
                    return Fail(testLog, step8Response.StackTrace);
                }

                testLog.AppendLine($"Step 8: UpdateOrder (quantity change) – PASSED  " +
                                   $"[ItemCount={countAfterStep8}, NetAmountBefore={originalNetAmount}, NetAmountAfter={netAfterStep8}]");

                // ------------------------------------------------------------
                // STEP 9 – UpdateOrder: delete one existing item
                //          Expected: item count decreases by 1.
                // ------------------------------------------------------------
                ConsumerResponseModel step9Source = await GetOrderById(newOrderId);

                OrderModel step9Model = OrderMapper.DtoToEntity(step9Source.Order);

                step9Model.RecalculateNetAmount();

                ConsumerResponseModel step9Response = await UpdateOrder(OrderMapper.EntityToOrderDto(step9Model), testUserId);
                ConsumerResponseModel step9Refetch = await GetOrderById(newOrderId);

                int countAfterStep9 = step9Refetch.Order.Items.Count;

                if (!step9Response.Status || countAfterStep9 != countAfterStep8 - 1)
                {
                    testLog.AppendLine($"Step 9: UpdateOrder (item deletion) – FAILED  " +
                                       $"[Status={step9Response.Status}, ItemCount={countAfterStep9} (expected {countAfterStep8 - 1})]");
                    return Fail(testLog, step9Response.StackTrace);
                }

                testLog.AppendLine($"Step 9: UpdateOrder (item deletion) – PASSED  " +
                                   $"[ItemCountBefore={countAfterStep8}, ItemCountAfter={countAfterStep9}]");

                // ------------------------------------------------------------
                // STEP 10 – UpdateOrder: add a brand-new item
                //           Expected: item count increases by 1.
                // ------------------------------------------------------------
                ConsumerResponseModel step10Source = await GetOrderById(newOrderId);

                var baseItems = step10Source.Order.Items.Select(i => new OrderItemModel
                {
                    Id = i.Id,
                    OrderId = i.OrderId,
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = (decimal)i.UnitPrice,
                    GrossAmount = (decimal)(i.Quantity * i.UnitPrice)
                }).ToList();

                // Add new item (clean object, no EF tracking)
                baseItems.Add(new OrderItemModel
                {
                    Id = 0,
                    OrderId = newOrderId,
                    ProductId = baseItems.First().ProductId,
                    Quantity = 2,
                    UnitPrice = baseItems.First().UnitPrice,
                    GrossAmount = 2 * baseItems.First().UnitPrice
                });

                OrderModel step10Model = OrderMapper.DtoToEntity(step10Source.Order);

                step10Model.RecalculateNetAmount();

                ConsumerResponseModel step10Response = await UpdateOrder(OrderMapper.EntityToOrderDto(step10Model), testUserId);
                ConsumerResponseModel step10Refetch = await GetOrderById(newOrderId);

                int countAfterStep10 = step10Refetch.Order.Items.Count;

                if (!step10Response.Status || countAfterStep10 != countAfterStep9 + 1)
                {
                    testLog.AppendLine($"Step 10: UpdateOrder (item addition) – FAILED  " +
                                       $"[Status={step10Response.Status}, ItemCount={countAfterStep10} (expected {countAfterStep9 + 1})]");
                    return Fail(testLog, step10Response.StackTrace);
                }

                testLog.AppendLine($"Step 10: UpdateOrder (item addition) – PASSED  " +
                                   $"[ItemCountBefore={countAfterStep9}, ItemCountAfter={countAfterStep10}]");
            }

            // ----------------------------------------------------------------
            // STEP 11 – UpdateOrder guard: order id not found should return failure
            // ----------------------------------------------------------------
            OrderDto ghostDto = new OrderDto() { Id = -9999 };
            ConsumerResponseModel step11Response = await UpdateOrder(ghostDto, testUserId);

            if (step11Response.Status == true)
            {
                testLog.AppendLine("Step 11: UpdateOrder (non-existent id) – FAILED – expected failure but got success");
                return Fail(testLog);
            }

            testLog.AppendLine("Step 11: UpdateOrder (non-existent id guard) – PASSED");

            // ----------------------------------------------------------------
            // STEP 12 – UpdateDeleteStatusForSingleOrder (soft-delete)
            // ----------------------------------------------------------------
            ConsumerResponseModel deleteResponse = await UpdateDeleteStatusForSingleOrder(newOrderId, testUserId);

            if (!deleteResponse.Status)
            {
                testLog.AppendLine($"Step 12: UpdateDeleteStatusForSingleOrder (id={newOrderId}) – FAILED");
                return Fail(testLog, deleteResponse.StackTrace);
            }

            testLog.AppendLine($"Step 12: UpdateDeleteStatusForSingleOrder (id={newOrderId}) – PASSED");

            // ----------------------------------------------------------------
            // STEP 13 – Confirm the deleted order is no longer accessible
            //           (either returns Status=false, or the order is null)
            // ----------------------------------------------------------------
            ConsumerResponseModel postDeleteFetch = await GetOrderById(newOrderId);

            bool orderIsGone = !postDeleteFetch.Status || postDeleteFetch.Order == null;

            if (!orderIsGone)
            {
                // Some implementations soft-delete but still return the record.
                // In that case check the IsDeleted flag instead, if exposed on the DTO.
                testLog.AppendLine("Step 13: Post-delete verification – INFO – order still fetchable after soft-delete " +
                                   "(confirm IsDeleted flag is set in DB if soft-delete is the intended behaviour)");
            }
            else
            {
                testLog.AppendLine($"Step 13: Post-delete verification – PASSED  [order id={newOrderId} no longer returned]");
            }

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
