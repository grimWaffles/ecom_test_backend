using API_Gateway.Helpers;
using ApiGateway.Protos;
using Confluent.Kafka;
using Google.Protobuf.WellKnownTypes;
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

        //Custom Function
        Task<OrderResponse> GenerateCustomManualOrder();
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
            KafkaProducerResult result = await _kafkaEventProducer.ProduceEventAsync(
                order_create_topic,
                request.Order.Id.ToString(),
                JsonSerializer.Serialize(request.Order));

            return new OrderResponse()
            {
                Status = result.Status,
                Message = result.ErrorMessage,
                Order = request.Order
            };
        }

        public async Task<OrderResponse> UpdateOrderAsync(UpdateOrderRequest request)
        {
            KafkaProducerResult result = await _kafkaEventProducer.ProduceEventAsync(
                order_update_topic,
                request.Order.Id.ToString(),
                JsonSerializer.Serialize(request.Order));

            return new OrderResponse()
            {
                Status = result.Status,
                Message = result.ErrorMessage,
                Order = request.Order
            };
        }

        public async Task<OrderResponse> GenerateCustomManualOrder()
        {
            CustomConverters converter = new CustomConverters();

            DateTime startDate = DateTime.Parse("2025-02-01");
            DateTime endDate = DateTime.Parse("2025-02-01");

            OrderListRequest request = new OrderListRequest()
            {
                PageNumber = 1,
                PageSize = 10,
                StartDate = converter.ConvertDateTimeToGoogleTimeStamp(startDate),
                EndDate = converter.ConvertDateTimeToGoogleTimeStamp(endDate),
                UserId = 1
            };

            OrderListResponse response = new OrderListResponse();
            List<Order> orderList = new List<Order>();

            try
            {
                response = await GetAllOrdersAsync(request);
                orderList = response.Orders.ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to fetch orders");
            }

            if (orderList.Count > 0)
            {
                await FireAllOrderProduceEvents(orderList);
            }

            return new OrderResponse()
            {
                Status = true,
                Message = "Order processing",
                Order = new Order()
            };
        }

        private async Task<bool> FireAllOrderProduceEvents(List<Order> orders)
        {
            string topic = "order-create";
            try
            {
                List<Task> tasks = new List<Task>();

                foreach (Order order in orders)
                {
                    tasks.Add(new Task(async () => {
                        await _kafkaEventProducer.ProduceEventAsync(topic, order.Id.ToString(), JsonSerializer.Serialize(order));
                    }));
                }

                await Task.WhenAll(tasks);  

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
