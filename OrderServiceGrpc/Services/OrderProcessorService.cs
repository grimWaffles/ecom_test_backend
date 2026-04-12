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
    public interface IOrderProcessorService
    {
        Task<ConsumerResponseModel> CreateOrder(OrderDto dto, int userId);
        Task<ConsumerResponseModel> UpdateOrder(OrderDto model, int userId);
        Task<ConsumerResponseModel> UpdateDeleteStatusForSingleOrder(int orderId, int userId);
        Task<ConsumerResponseModel> GetAllOrders(DateTime startDate, DateTime endDate, int pageSize, int pageNumber, int userId);
        Task<ConsumerResponseModel> GetOrderById(int orderId);
        Task<ConsumerResponseModel> TestOrderProcessorService();
    }
    public class OrderProcessorService : IOrderProcessorService
    {
        private readonly IOrderRepository _repo;
        private readonly IUnitOfWork _uow;
        public OrderProcessorService(IOrderRepository orderRepository, IUnitOfWork unitOfWork)
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
                        EventType = "OrderDeleted",
                        Topic = "order-delete-success",
                        PartitionKey = dto.Id.ToString(),
                        Payload = System.Text.Json.JsonSerializer.Serialize(new { orderId = dto.Id, userId = userId }),
                        Headers = "{}",
                        StatusId = 1, // Assuming 1 is the status for 'Pending'
                        RetryCount = 0,
                        CreatedAt = DateTime.UtcNow,
                        ScheduledAt = DateTime.UtcNow
                    };

                    await _uow.Outbox.CreateAsync(outboxEntry);

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

        //Integration Test for the service + repo
        public async Task<ConsumerResponseModel> TestOrderProcessorService()
        {
            /* Test Process for OrderGrpcService
             * 1) Get list of orders
             * 2) Select 1 Id and fetch the single order using the function
             * 3) Insert the order and check if the Insert is successful
             * 4) Update the new order and check if the Update is successful
             * 5) Delete the order and check if the delete is successful
             */

            StringBuilder testLog = new StringBuilder();

            DateTime startDate = new DateTime(2021, 01, 01), endDate = new DateTime(2026, 12, 31);
            int pageSize = 10, pageNumber = 1;

            //Step 1
            ConsumerResponseModel response = await GetAllOrders(startDate, endDate, pageSize, pageNumber, 0);

            if (response.ListOfOrders.Count() > 0 && response.TotalOrders > 0 && response.TotalPages > 0)
            {
                testLog.AppendLine("Step 1: GetAllOrders - Passed");
            }
            else
            {
                testLog.AppendLine("Step 1: GetAllOrders - Failed");
                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = testLog.ToString(),
                    StackTrace = response.StackTrace,
                };
            }

            //Step 2 
            int orderId = response.ListOfOrders.Where(x => x.Items.Count > 2).First().Id;

            if (orderId == 0)
            {
                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = testLog.ToString(),
                    StackTrace = "Failed to get order with more than 2 items",
                };
            }

            ConsumerResponseModel singleOrderResponse = await GetOrderById(orderId);

            if (singleOrderResponse.Order != null && singleOrderResponse.Order.Id == orderId)
            {
                testLog.AppendLine("Step 2: GetOrderById - Passed");
            }
            else
            {
                testLog.AppendLine("Step 2: GetOrderById - Failed");
                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = testLog.ToString(),
                    StackTrace = singleOrderResponse.StackTrace,
                };
            }

            //Step 3
            OrderDto dto = singleOrderResponse.Order;
            OrderModel model = OrderMapper.DtoToEntity(dto);

            model.Id = 0;
            model.OrderDate = DateTime.Now;

            ConsumerResponseModel orderAddedResponse = await CreateOrder(OrderMapper.EntityToOrderDto(model), 1);

            if (orderAddedResponse.Status == true && orderAddedResponse.InsertedOrderId > 0)
            {
                testLog.AppendLine("Step 3: CreateOrder - Passed");
            }
            else
            {
                testLog.AppendLine("Step 3: CreateOrder - Failed");
                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = testLog.ToString(),
                    StackTrace = orderAddedResponse.StackTrace,
                };
            }

            //Step 4
            decimal initialItemCount = 0, finalItemCount = 0, initialNetAmount = 0, finalNetAmount = 0;

            ConsumerResponseModel insertedDto = await GetOrderById(orderAddedResponse.InsertedOrderId);

            OrderDto dtoToUpdate = insertedDto.Order;

            OrderModel modelToUpdate = OrderMapper.DtoToEntity(dtoToUpdate);

            initialNetAmount = modelToUpdate.NetAmount; initialItemCount = modelToUpdate.OrderItems.Count;

            OrderItemModel firstitem = modelToUpdate.OrderItems[0];
            firstitem.Quantity += 12; firstitem.Id = 0; firstitem.OrderId = dtoToUpdate.Id;

            modelToUpdate.OrderItems = modelToUpdate.OrderItems.Where(x => x.Id != firstitem.Id).ToList();

            modelToUpdate.OrderItems.ForEach(x => x.Quantity += 10);

            modelToUpdate.OrderItems.Add(firstitem);

            modelToUpdate.RecalculateNetAmount();

            ConsumerResponseModel updateOrderResponse = await UpdateOrder(OrderMapper.EntityToOrderDto(modelToUpdate), 1);

            ConsumerResponseModel updatedDto = await GetOrderById(modelToUpdate.Id);

            finalItemCount = updatedDto.Order.Items.Count; finalNetAmount = (decimal)updatedDto.Order.NetAmount;

            if (updateOrderResponse.Status == true && (finalItemCount == initialItemCount) && (finalNetAmount != initialNetAmount))
            {
                testLog.AppendLine("Step 4: UpdateOrder - Passed");
            }
            else
            {
                testLog.AppendLine("Step 4: UpdateOrder - Failed");
                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = testLog.ToString(),
                    StackTrace = updateOrderResponse.StackTrace,
                };
            }

            //Step 5
            ConsumerResponseModel deleteOrderResponse = await UpdateDeleteStatusForSingleOrder(orderAddedResponse.InsertedOrderId, 1);

            if (deleteOrderResponse.Status == true)
            {
                testLog.AppendLine("Step 5: DeleteOrder - Passed");
            }
            else
            {
                testLog.AppendLine("Step 5: DeleteOrder - Failed");
                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = testLog.ToString(),
                    StackTrace = deleteOrderResponse.StackTrace,
                };
            }

            return new ConsumerResponseModel()
            {
                Status = true,
                Message = testLog.ToString()
            };
        }
    }
}
