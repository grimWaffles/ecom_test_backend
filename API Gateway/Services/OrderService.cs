using API_Gateway.Helpers;
using API_Gateway.Models;
using ApiGateway.Protos;
using Confluent.Kafka;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
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

        //Custom Function
        Task<OrderResponse> GenerateCustomManualOrder();
    }

    public class OrderGrpcClient : IOrderGrpcClient
    {
        private readonly OrderGrpcService.OrderGrpcServiceClient _orderClient;
        private readonly IKafkaEventProducer _kafkaEventProducer;

        private readonly string order_create_topic = "order-create";
        private readonly string order_update_topic = "order-update";

        private readonly MicroServiceUrl _urls;

        public OrderGrpcClient(IOptions<MicroServiceUrl> microserviceUrls, IKafkaEventProducer kafkaEventProducer)
        {
            //gRPC 
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

            //Kafka
            _kafkaEventProducer = kafkaEventProducer;
        }

        //gRPC Endpoints
        public async Task<OrderResponse> GetOrderByIdAsync(OrderIdRequest request)
            => await _orderClient.GetOrderByIdAsync(request);

        public async Task<OrderListResponse> GetOrdersByUserAsync(OrderListRequest request)
            => await _orderClient.GetOrdersByUserAsync(request);

        public async Task<OrderListResponse> GetAllOrdersAsync(OrderListRequest request)
            => await _orderClient.GetAllOrdersAsync(request);

        public async Task<OrderResponse> DeleteOrderAsync(DeleteOrderRequest request)
            => await _orderClient.DeleteOrderAsync(request);

        //Kafka Producers
        public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
        {
            //KafkaProducerResult result = await _kafkaEventProducer.ProduceEventAsync(
            //    order_create_topic,
            //    request.Order.Id.ToString(),
            //    JsonSerializer.Serialize(request.Order));

            //return new OrderResponse()
            //{
            //    Status = result.Status,
            //    Message = result.ErrorMessage,
            //    Order = request.Order
            //};
            OrderResponse res = await _orderClient.CreateOrderAsync(request);
            return res;
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
                    #region Multiple Order Inserting Using Threads & Tasks
                    //List<Order> list1 = orderList.Take(orderList.Count / 2).ToList();
                    //List<Order> list2 = orderList.Skip(orderList.Count / 2).Take(orderList.Count / 2).ToList();

                    //DateTime startTime = DateTime.Now;
                    //Task t1 = Task.Run(async () =>
                    //{
                    //    await FireAllOrderProduceEvents(list1);
                    //});

                    //Task t2 = Task.Run(async () =>
                    //{
                    //    await FireAllOrderProduceEvents(list2);
                    //});

                    //await Task.WhenAll(t1, t2);
                    //return new OrderResponse()
                    //{
                    //    Status = true,
                    //    Message = $"Produced messages for {orderList.Count} orders.",
                    //    Order = null
                    //};
                    #endregion

                    #region Produce Events Sequentially

                    //await FireAllOrderProduceEvents(orderList);
                    //return new OrderResponse()
                    //{
                    //    Status = true,
                    //    Message = $"Produced messages for {orderList.Count} orders.",
                    //    Order = null
                    //};
                    #endregion

                    #region Call the create function for the outbox approach
                    Order orderToCreate = orderList.Where(o=> !o.IsDeleted && o.Items.Count()>2 ).FirstOrDefault();
                    CreateOrderRequest createOrderRequest = new CreateOrderRequest() { Order = orderToCreate, UserId = 1 };
                    
                    OrderResponse fnResponse = await CreateOrderAsync(createOrderRequest);

                    return new OrderResponse()
                    {
                        Status = fnResponse.Status,
                        Message = fnResponse.Message,
                        Order = fnResponse.Order,
                    };
                    #endregion
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

        private async Task<bool> FireAllOrderProduceEvents(List<Order> orders)
        {
            string topic = "order-create";
            try
            {
                //List<Task> tasks = new List<Task>();

                foreach (Order order in orders)
                {
                    //tasks.Add(new Task(async () => {
                    //    await _kafkaEventProducer.ProduceEventAsync(topic, order.Id.ToString(), JsonSerializer.Serialize(order));
                    //}));
                    CreateOrderRequest request = new CreateOrderRequest() { Order = order, UserId = 1 };
                    string payload = JsonSerializer.Serialize(request);

                    await _kafkaEventProducer.ProduceEventAsync(topic, order.Id.ToString(), payload);
                }

                //await Task.WhenAll(tasks);  

                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
