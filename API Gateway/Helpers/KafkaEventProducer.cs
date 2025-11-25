using API_Gateway.Models;
using Confluent.Kafka;
using System.Text.Json;

namespace API_Gateway.Helpers
{
    public interface IKafkaEventProducer
    {
        Task<Tuple<bool, string>> ProduceEventAsync(string topic, string key, string payload, int? partition = null, CancellationToken token = default);
        //Task<bool> PublishOrderEvent();
    }

    public class KafkaEventProducer : IKafkaEventProducer
    {
        private readonly string _bootstrapServer;
        private readonly IConfiguration _config;
        private readonly ProducerConfig _producerConfig;
        private readonly IProducer<string, string> _producer;

        //For Manual Partitioning support
        private int _partitionCounter = 0;
        private readonly object _lock = new object();
        private readonly int _totalPartitions = 6;

        private int GetNextPartition()
        {
            lock (_lock)
            {
                return (_partitionCounter + 1) % _totalPartitions;
            }
        }

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

            _producer = new ProducerBuilder<string, string>(_producerConfig).Build();
        }

        public async Task<Tuple<bool, string>> ProduceEventAsync
            (string topic, string key, string payload, int? partition = null, CancellationToken token = default)
        {
            try
            {
                Message<string, string> messageToSend = new Message<string, string>
                { Key = key, Value = payload };

                int selectedPartition = partition ?? GetNextPartition();

                TopicPartition target = new TopicPartition(topic, new Partition(selectedPartition));

                await _producer.ProduceAsync(target, messageToSend, token);

                return new Tuple<bool, string>(true, "Success");
            }
            catch (Exception e)
            {
                return new Tuple<bool, string>(false, e.Message);
            }
        }
    }
}
