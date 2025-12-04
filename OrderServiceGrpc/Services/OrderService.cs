
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OrderServiceGrpc.Helpers.cs;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Repository;

namespace OrderServiceGrpc.Services
{
    public class OrderService : OrderGrpcService.OrderGrpcServiceBase
    {
        private readonly IOrderRepository _repo;

        public OrderService(IOrderRepository orderRepository)
        {
            _repo = orderRepository;
        }

        public override async Task<OrderResponse> CreateOrder(CreateOrderRequest request, ServerCallContext context)
        {
            int userId = 1;

            if (!Validate())
            {
                return new OrderResponse() { Message = "Failed to validate", Status = false };
            }

            bool orderAdded = await _repo.AddOrder(OrderMessageModelConverter.ToModel(request.Order), userId);

            return new OrderResponse()
            {
                Status = true,
                Message = "Added successfully"
            };
        }

        public override async Task<OrderResponse> UpdateOrder(UpdateOrderRequest request, ServerCallContext context)
        {
            int userId = 1;

            if (!Validate())
            {
                return new OrderResponse() { Message = "Failed to validate", Status = false };
            }

            OrderModel requestModel = OrderMessageModelConverter.ToModel(request.Order);
            OrderModel dbModel = await _repo.GetOrderById(requestModel.Id);

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

            bool orderAdded = await _repo.UpdateOrder(requestModel, addList, deleteList, updateList, userId);

            return new OrderResponse()
            {
                Status = true,
                Message = "Updated successfully"
            };
        }

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

        public override async Task<OrderResponse> DeleteOrder(DeleteOrderRequest request, ServerCallContext context)
        {
            int userId = 1;

            if (!Validate())
            {
                return new OrderResponse() { Message = "Failed to validate", Status = false };
            }

            bool orderAdded = await _repo.DeleteOrder(request.Id, userId);

            return new OrderResponse()
            {
                Status = true,
                Message = "Deleted successfully"
            };
        }

        public override async Task<OrderListResponse> GetAllOrders(OrderListRequest request, ServerCallContext context)
        {
            Tuple<int, int, List<OrderModel>> result = await _repo.GetAllOrdersWithPagination(request);

            if (result == null) { return new OrderListResponse() { Message = "Failed to get orders", Status = false }; }

            OrderListResponse response = new OrderListResponse()
            {
                Status = true,
                Message = "Success",
                TotalPages = result.Item1,
                TotalOrders = result.Item2
            };

            try
            {
                List<Order> orders = result.Item3.Select(m => OrderMessageModelConverter.ToMessage(m)).ToList();
                response.Orders.AddRange(orders);
                return response;
            }
            catch (Exception e)
            {
                return new OrderListResponse() { Message = "Failed to get orders", Status = false };
            }
        }

        public override async Task<OrderResponse> GetOrderById(OrderIdRequest request, ServerCallContext context)
        {
            OrderModel model = await _repo.GetOrderById(request.Id);

            if (model == null) { return new OrderResponse() { Message = "Failed to get order", Status = false }; }

            return new OrderResponse()
            {
                Status = true,
                Message = "Success",
                Order = OrderMessageModelConverter.ToMessage(model)
            };
        }

        public override Task<OrderListResponse> GetOrdersByUser(UserIdRequest request, ServerCallContext context)
        {
            return base.GetOrdersByUser(request, context);
        }

        private bool Validate()
        {
            return true;
        }
    }
}
