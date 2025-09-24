using API_Gateway.Helpers;
using ApiGateway.Protos;
using Confluent.Kafka;
using Grpc.Net.Client;
using System.Text.Json;
namespace API_Gateway.Services
{
    public interface IOrderGrpcClient
    {
        Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request);
        Task<OrderResponse> GetOrderByIdAsync(OrderIdRequest request);
        Task<OrderListResponse> GetOrdersByUserAsync(UserIdRequest request);
        Task<OrderListResponse> GetAllOrdersAsync(OrderListRequest request);
        Task<OrderResponse> UpdateOrderAsync(UpdateOrderRequest request);
        Task<OrderResponse> DeleteOrderAsync(DeleteOrderRequest request);
    }
    public class OrderGrpcClient : IOrderGrpcClient
    {
        private readonly OrderGrpcService.OrderGrpcServiceClient _orderClient;
        private readonly IKafkaEventProducer _kafkaEventProducer;

        private readonly string order_create_topic = "order-create";
        private readonly string order_update_topic = "order-update";

        public OrderGrpcClient(IConfiguration configuration, IKafkaEventProducer kafkaEventProducer)
        {
            //gRPC 
            string serviceUrl = configuration["Microservices:orderService"] ?? "";

            var httpHandler = new HttpClientHandler
            {
                // This is optional and should be used only in development for insecure certs
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            var channel = GrpcChannel.ForAddress(serviceUrl, new GrpcChannelOptions
            {
                HttpHandler = httpHandler
            });

            _orderClient = new OrderGrpcService.OrderGrpcServiceClient(channel);

            //Kafka
            _kafkaEventProducer = kafkaEventProducer;
        }

        //gRPC Endpoints
        public async Task<OrderResponse> GetOrderByIdAsync(OrderIdRequest request)
            => await _orderClient.GetOrderByIdAsync(request);

        public async Task<OrderListResponse> GetOrdersByUserAsync(UserIdRequest request)
            => await _orderClient.GetOrdersByUserAsync(request);

        public async Task<OrderListResponse> GetAllOrdersAsync(OrderListRequest request)
            => await _orderClient.GetAllOrdersAsync(request);

        public async Task<OrderResponse> DeleteOrderAsync(DeleteOrderRequest request)
            => await _orderClient.DeleteOrderAsync(request);

        //Kafka Producers
        public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
        {
            Tuple<bool, string> result = await _kafkaEventProducer.ProduceEventAsync(
                order_create_topic,
                request.Order.Id.ToString(),
                JsonSerializer.Serialize(request.Order));

            return new OrderResponse()
            {
                Status = result.Item1,
                Message = result.Item2,
                Order = request.Order
            };
        }

        public async Task<OrderResponse> UpdateOrderAsync(UpdateOrderRequest request)
        {
            Tuple<bool, string> result = await _kafkaEventProducer.ProduceEventAsync(
                order_update_topic,
                request.Order.Id.ToString(),
                JsonSerializer.Serialize(request.Order));

            return new OrderResponse()
            {
                Status = result.Item1,
                Message = result.Item2,
                Order = request.Order
            };
        }
    }
}
