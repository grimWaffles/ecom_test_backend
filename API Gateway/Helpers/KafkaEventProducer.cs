using API_Gateway.Models;
using Confluent.Kafka;
using System.Text.Json;

namespace API_Gateway.Helpers
{
    public interface IKafkaEventProducer
    {
        Task<Tuple<bool, string>> ProduceEventAsync(string topic, string key, string payload);
        bool PublishOrderEvent();
    }

    public class KafkaEventProducer : IKafkaEventProducer
    {
        private readonly string _bootstrapServer;
        private readonly IConfiguration _config;
        private readonly ProducerConfig _producerConfig;

        public KafkaEventProducer(IConfiguration configuration)
        {
            _config = configuration;
            _bootstrapServer = _config["Kafka:BootstrapServer"] ?? "";
            _producerConfig = new ProducerConfig
            {
                BootstrapServers = _bootstrapServer,
                Acks = Acks.All,              // stronger delivery guarantees
                EnableIdempotence = true,     // safe retries, no duplicates
                MessageTimeoutMs = 5000
            };
        }

        public async Task<Tuple<bool, string>> ProduceEventAsync(string topic, string key, string payload)
        {
            try
            {
                using (IProducer<string, string> producer = new ProducerBuilder<string, string>(_producerConfig).Build())
                {
                    await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = payload });
                }

                return new Tuple<bool, string>(true, "Success");
            }
            catch (Exception e)
            {
                return new Tuple<bool, string>(false, e.Message);
            }
        }

        public bool PublishOrderEvent()
        {
            using (var producer = new ProducerBuilder<string, string>(_producerConfig).Build())
            {
                var orderEvent = new OrderCreatedEvent()
                {
                    OrderId = 16969,
                    CustomerId = 123,
                    Amount = 250.75m,
                    CreatedAt = DateTime.UtcNow
                };

                string eventJson = JsonSerializer.Serialize(orderEvent);

                for (int i = 0; i < 500; i++)
                {
                    try
                    {
                        producer.ProduceAsync("order-create", new Message<string, string> { Key = orderEvent.OrderId.ToString(), Value = eventJson });
                    }
                    catch (Exception e)
                    {
                        return false;
                    }

                    Task.Delay(100);
                }
            }
            return true;
        }
    }
}
