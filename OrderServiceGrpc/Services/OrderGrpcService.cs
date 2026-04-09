
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OrderServiceGrpc.Helpers.Converters;
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
            string validationError = ValidateOrder(request.Order);
            if (validationError != null)
            {
                return new OrderResponse { Status = false, Message = validationError };
            }

            if (request.UserId <= 0)
            {
                return new OrderResponse { Status = false, Message = "Invalid user ID" };
            }

            ConsumerResponseModel response = await _service.CreateOrder(OrderMapper.ProtoToDto(request.Order), request.UserId);
            return new OrderResponse
            {
                Status = response.Status,
                Message = response.Message,
                Order = OrderMapper.DtoToProto(response.Order)
            };
        }

        public override async Task<OrderResponse> UpdateOrder(UpdateOrderRequest request, ServerCallContext context)
        {
            if (request.Order.Id <= 0)
            {
                return new OrderResponse { Status = false, Message = "Invalid order ID" };
            }

            string validationError = ValidateOrder(request.Order);
            if (validationError != null)
            {
                return new OrderResponse { Status = false, Message = validationError };
            }

            if (request.UserId <= 0)
            {
                return new OrderResponse { Status = false, Message = "Invalid user ID" };
            }

            ConsumerResponseModel response = await _service.UpdateOrder(OrderMapper.ProtoToDto(request.Order), request.UserId);
            return new OrderResponse
            {
                Status = response.Status,
                Message = response.Message,
                Order = OrderMapper.DtoToProto(response.Order)
            };
        }

        public override async Task<OrderResponse> DeleteOrder(DeleteOrderRequest request, ServerCallContext context)
        {
            if (request.Id <= 0)
            {
                return new OrderResponse { Status = false, Message = "Invalid order ID" };
            }

            if (request.UserId <= 0)
            {
                return new OrderResponse { Status = false, Message = "Invalid user ID" };
            }

            ConsumerResponseModel response = await _service.UpdateDeleteStatusForSingleOrder(request.Id, request.UserId);
            return new OrderResponse
            {
                Status = response.Status,
                Message = response.Message,
                Order = new Order()
            };
        }

        public override async Task<OrderListResponse> GetAllOrders(OrderListRequest request, ServerCallContext context)
        {
            OrderListResponse validationResponse = ValidatePagedRequests(request);

            if(validationResponse != null)
            {
                return validationResponse;
            }

            DateTime start = DateTimeHelper.ConvertTimestampToDateTime(request.StartDate);
            DateTime end = DateTimeHelper.ConvertTimestampToDateTime(request.EndDate);

            ConsumerResponseModel response = await _service.GetAllOrders(start, end, request.PageSize, request.PageNumber, 0);

            OrderListResponse orderListResponse = new OrderListResponse
            {
                Status = response.Status,
                Message = response.Message,
                TotalOrders = response.TotalOrders,
                TotalPages = response.TotalPages
            };

            orderListResponse.Orders.AddRange(response.ListOfOrders.Select(m => OrderMapper.DtoToProto(m)).ToList());
            return orderListResponse;
        }

        public override async Task<OrderResponse> GetOrderById(OrderIdRequest request, ServerCallContext context)
        {
            if (request.Id <= 0)
            {
                return new OrderResponse { Status = false, Message = "Invalid order ID" };
            }

            ConsumerResponseModel response = await _service.GetOrderById(request.Id);
            return new OrderResponse
            {
                Status = response.Status,
                Message = response.Message,
                Order = OrderMapper.DtoToProto(response.Order)
            };
        }

        public override async Task<OrderListResponse> GetOrdersByUser(OrderListRequest request, ServerCallContext context)
        {
            OrderListResponse validationResponse = ValidatePagedRequests(request);

            if (validationResponse != null)
            {
                return validationResponse;
            }

            if (request.UserId <= 0)
            {
                return new OrderListResponse { Status = false, Message = "Invalid user ID" };
            }

            DateTime start = DateTimeHelper.ConvertTimestampToDateTime(request.StartDate);
            DateTime end = DateTimeHelper.ConvertTimestampToDateTime(request.EndDate);

            ConsumerResponseModel response = await _service.GetAllOrders(start, end, request.PageSize, request.PageNumber, request.UserId);

            OrderListResponse orderListResponse = new OrderListResponse
            {
                Status = response.Status,
                Message = response.Message,
                TotalOrders = response.TotalOrders,
                TotalPages = response.TotalPages
            };

            orderListResponse.Orders.AddRange(response.ListOfOrders.Select(m => OrderMapper.DtoToProto(m)).ToList());
            return orderListResponse;
        }

        public override async Task<OrderResponse> TestOrderGrpcService(Empty request, ServerCallContext context)
        {
            ConsumerResponseModel response = await _service.TestOrderProcessorService();
            return new OrderResponse
            {
                Status = response.Status,
                Message = response.Message
            };
        }

        // Validates fields shared between Create and Update
        private string ValidateOrder(Order order)
        {
            if (order == null)
                return "Order cannot be null";

            if (order.UserId <= 0)
                return "Invalid user ID on order";

            if (string.IsNullOrWhiteSpace(order.Status))
                return "Order status is required";

            if (order.NetAmount < 0)
                return "Net amount cannot be negative";

            if (order.Items == null || order.Items.Count == 0)
                return "Order must contain at least one item";

            foreach (var item in order.Items)
            {
                string itemError = ValidateOrderItem(item);
                if (itemError != null)
                    return itemError;
            }

            return null;
        }

        private string ValidateOrderItem(OrderItem item)
        {
            if (item.ProductId <= 0)
                return "Invalid product ID on order item";

            if (item.Quantity <= 0)
                return "Order item quantity must be greater than 0";

            if (item.UnitPrice < 0)
                return "Order item unit price cannot be negative";

            if (item.GrossAmount < 0)
                return "Order item gross amount cannot be negative";

            if (string.IsNullOrWhiteSpace(item.Status))
                return "Order item status is required";

            return null;
        }

        private OrderListResponse ValidatePagedRequests(OrderListRequest request)
        {
            if (request.PageSize <= 0)
            {
                return new OrderListResponse { Status = false, Message = "Page size must be greater than 0" };
            }

            if (request.PageNumber <= 0)
            {
                return new OrderListResponse { Status = false, Message = "Page number must be greater than 0" };
            }

            if (request.StartDate != null && request.EndDate != null)
            {
                DateTime startDate = DateTimeHelper.ConvertTimestampToDateTime(request.StartDate);
                DateTime endDate = DateTimeHelper.ConvertTimestampToDateTime(request.EndDate);

                if (startDate > endDate)
                {
                    return new OrderListResponse { Status = false, Message = "Start date cannot be greater than end date" };
                }
            }

            return null;
        }
    }
}
