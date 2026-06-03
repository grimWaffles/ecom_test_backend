using API_Gateway.Helpers;
using API_Gateway.Models;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
namespace API_Gateway.Services
{
    public interface IOrderGrpcClient
    {
        Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request);
        Task<OrderResponse> GetOrderByIdAsync(OrderIdRequest request);
        Task<OrderListResponse> GetOrdersByUserAsync(OrderListRequest request);
        Task<OrderListResponse> GetAllOrdersAsync(OrderListRequest request);
        Task<OrderResponse> UpdateOrderAsync(UpdateOrderRequest request);
        Task<OrderResponse> DeleteOrderAsync(DeleteOrderRequest request);
        Task<OrderResponse> TestOrderServiceAsync(Empty empty);
        Task<OrderHealthCheckMessage> TestOrderServiceHealth();

        //Custom Function
        Task<OrderResponse> GenerateCustomManualOrder();
    }

    public class OrderGrpcClient : IOrderGrpcClient
    {
        private readonly OrderGrpcService.OrderGrpcServiceClient _orderClient;
        private readonly MicroServiceUrl _urls;
        private readonly ILogger<OrderGrpcClient> _logger;
        private readonly DateTime rpcCallLimit = DateTime.Now.AddSeconds(2);

        public OrderGrpcClient(IOptions<MicroServiceUrl> microserviceUrls, ILogger<OrderGrpcClient> logger)
        {
            //gRPC 
            _logger = logger;
            _urls = microserviceUrls.Value;

            if (string.IsNullOrEmpty(_urls.GetOrderServiceUrl()))
            {
                throw new ArgumentException("gRPC service URL not configured in appsettings.json");
            }

            var httpHandler = new HttpClientHandler
            {
                // This is optional and should be used only in development for insecure certs
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            var channel = GrpcChannel.ForAddress(_urls.GetOrderServiceUrl(), new GrpcChannelOptions
            {
                HttpHandler = httpHandler
            });

            _orderClient = new OrderGrpcService.OrderGrpcServiceClient(channel);
        }

        //gRPC Endpoints
        public async Task<OrderResponse> GetOrderByIdAsync(OrderIdRequest request)
        {
            try
            {
                return await _orderClient.GetOrderByIdAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while getting order by id");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while getting order by id");
                return null;
            }
        }

        public async Task<OrderListResponse> GetOrdersByUserAsync(OrderListRequest request)
        {
            try
            {
                return await _orderClient.GetOrdersByUserAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while getting orders by user");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while getting orders by user");
                return null;
            }
        }

        public async Task<OrderListResponse> GetAllOrdersAsync(OrderListRequest request)
        {
            try
            {
                return await _orderClient.GetAllOrdersAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while getting all orders");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while getting all orders");
                return null;
            }
        }

        public async Task<OrderResponse> DeleteOrderAsync(DeleteOrderRequest request)
        {
            try
            {
                return await _orderClient.DeleteOrderAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while deleting order");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while deleting order");
                return null;
            }
        }

        //Kafka Producers
        public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
        {
            try
            {
                return await _orderClient.CreateOrderAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while creating order");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while creating order");
                return null;
            }
        }

        public async Task<OrderResponse> UpdateOrderAsync(UpdateOrderRequest request)
        {
            try
            {
                return await _orderClient.UpdateOrderAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while updating order");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while updating order");
                return null;
            }
        }

        public async Task<OrderResponse> GenerateCustomManualOrder()
        {
            DateTime startDate = DateTime.Parse("2022-01-01");
            DateTime endDate = DateTime.Parse("2027-12-31");

            OrderListRequest request = new OrderListRequest()
            {
                PageNumber = 1,
                PageSize = 1,
                StartDate = CustomConverters.ConvertDateTimeToGoogleTimeStamp(startDate),
                EndDate = CustomConverters.ConvertDateTimeToGoogleTimeStamp(endDate),
                UserId = 1
            };

            OrderListResponse response = new OrderListResponse();
            List<Order> orderList = new List<Order>();

            try
            {
                response = await GetAllOrdersAsync(request);
                orderList = response.Orders.ToList();

                foreach (Order order in orderList)
                {
                    foreach (OrderItem item in order.Items)
                    {
                        item.IsDeleted = false;
                    }
                }
                if (orderList.Count > 0)
                {
                    Order orderToCreate = orderList.FirstOrDefault(o => !o.IsDeleted && o.Items.Count() > 2);

                    CreateOrderRequest createOrderRequest = new CreateOrderRequest() { Order = orderToCreate, UserId = 1 };

                    OrderResponse fnResponse = await _orderClient.CreateOrderAsync(createOrderRequest);//await CreateOrderAsync(createOrderRequest);

                    return new OrderResponse()
                    {
                        Status = fnResponse.Status,
                        Message = fnResponse.Message,
                        Order = fnResponse.Order,
                    };
                }

                return new OrderResponse()
                {
                    Status = false,
                    Message = $"No orders found",
                    Order = null
                };
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to fetch orders");
                return new OrderResponse()
                {
                    Status = false,
                    Message = $"Failed to fetch orders",
                    Order = null
                };
            }
        }

        public async Task<OrderResponse> TestOrderServiceAsync(Empty empty)
        {
            try
            {
                return await _orderClient.TestOrderGrpcServiceAsync(empty);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while testing order service");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while testing order service");
                return null;
            }
        }

        public async Task<OrderHealthCheckMessage> TestOrderServiceHealth()
        {
            try
            {
                return await _orderClient.TestOrderServiceHealthAsync(new Empty(), deadline: rpcCallLimit);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while testing order service health");
                return new OrderHealthCheckMessage() { Message = "Order service is down" };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while testing order service health");
                return new OrderHealthCheckMessage() { Message = "Order service is down" };
            }
        }
    }
}
