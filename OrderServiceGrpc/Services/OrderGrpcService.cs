
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OrderServiceGrpc.Helpers.cs;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Repository;

namespace OrderServiceGrpc.Services
{
    public class OrderGrpcService : Protos.OrderGrpcService.OrderGrpcServiceBase
    {
        private readonly IOrderProcessorService _service;

        public OrderGrpcService(IOrderProcessorService orderProcessorService)
        {
            _service = orderProcessorService;
        }

        public override async Task<OrderResponse> CreateOrder(CreateOrderRequest request, ServerCallContext context)
        {
            int userId = 1;

            if (!ValidateGrpcRequests())
            {
                return new OrderResponse() { Message = "Failed to validate", Status = false };
            }
            
            ProcessorResponseModel response = await _service.CreateOrder(OrderMessageModelConverter.ToModel(request.Order), userId);

            return new OrderResponse()
            {
                Status = response.Status,
                Message = response.Message,
                Order = OrderMessageModelConverter.ToMessage(response.Order ?? new OrderModel())
            };
        }

        public override async Task<OrderResponse> UpdateOrder(UpdateOrderRequest request, ServerCallContext context)
        {
            int userId = 1;

            if (!ValidateGrpcRequests())
            {
                return new OrderResponse() { Message = "Failed to validate", Status = false };
            }

            ProcessorResponseModel response = await _service.UpdateOrder(OrderMessageModelConverter.ToModel(request.Order), userId);

            return new OrderResponse()
            {
                Status = response.Status,
                Message = response.Message,
                Order = OrderMessageModelConverter.ToMessage(response.Order ?? new OrderModel())
            };
        }

        public override async Task<OrderResponse> DeleteOrder(DeleteOrderRequest request, ServerCallContext context)
        {
            int userId = 1;

            if (!ValidateGrpcRequests())
            {
                return new OrderResponse() { Message = "Failed to validate", Status = false };
            }

            ProcessorResponseModel response = await _service.DeleteOrder(request.Id, userId);

            return new OrderResponse()
            {
                Status = response.Status,
                Message = response.Message,
                Order = OrderMessageModelConverter.ToMessage(response.Order ?? new OrderModel())
            };
        }

        public override async Task<OrderListResponse> GetAllOrders(OrderListRequest request, ServerCallContext context)
        {
            DateTime startDate = DateTimeHelper.ConvertTimestampToDateTime(request.StartDate);
            DateTime endDate = DateTimeHelper.ConvertTimestampToDateTime(request.EndDate);

            ProcessorResponseModel response = await _service.GetAllOrders(startDate, endDate, request.PageSize, request.PageNumber);
            OrderListResponse orderListResponse = new OrderListResponse()
            {
                Status = response.Status,
                Message = response.Message,
                TotalOrders = response.TotalOrders,
                TotalPages = response.TotalPages
            };

            orderListResponse.Orders.AddRange(response.ListOfOrders.Select(m=>OrderMessageModelConverter.ToMessage(m)).ToList());

            return orderListResponse;
        }

        public override async Task<OrderResponse> GetOrderById(OrderIdRequest request, ServerCallContext context)
        {
            ProcessorResponseModel response = await _service.GetOrderById(request.Id);

            return new OrderResponse()
            {
                Status = response.Status,
                Message = response.Message,
                Order = OrderMessageModelConverter.ToMessage(response.Order ?? new OrderModel())
            };
        }

        public override Task<OrderListResponse> GetOrdersByUser(UserIdRequest request, ServerCallContext context)
        {
            return base.GetOrdersByUser(request, context);
        }

        private bool ValidateGrpcRequests()
        {
            return true;
        }
    }
}
