
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OrderServiceGrpc.Helpers.cs;
using OrderServiceGrpc.Models;
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

            bool orderAdded = await _repo.AddOrder(OrderMapper.ToModel(request.Order), userId);

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

            bool orderAdded = await _repo.UpdateOrder(OrderMapper.ToModel(request.Order), userId);

            return new OrderResponse()
            {
                Status = true,
                Message = "Updated successfully"
            };
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
                List<Order> orders = result.Item3.Select(m=>OrderMapper.ToProto(m)).ToList();
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
            OrderModel model = await _repo.GetOrderById(request);

            if (model == null) { return new OrderResponse() { Message = "Failed to get order", Status = false }; }

            return new OrderResponse()
            {
                Status = true,
                Message = "Success",
                Order = OrderMapper.ToProto(model)
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
