using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.ConfigModels;
using System.Text.Json;

namespace OrderServiceGrpc.Kafka.Producers
{
    public interface IKafkaEventProducer
    {
        Task<bool> ProduceDlqEventAsync(string topic, DeadLetterQueueMessage deadLetterQueueMessage, CancellationToken stoppingToken);
        Task<bool> ProduceEventAsync(string topic, string key, string payload, CancellationToken stoppingToken);
    }

    public class KafkaEventProducer : IKafkaEventProducer, IDisposable, IAsyncDisposable
    {
        private readonly ILogger<KafkaEventProducer> _logger;
        private readonly IProducer<string, string> _producer; // Renamed: not DLQ-specific
        private bool _disposed;

        public KafkaEventProducer(
            ILogger<KafkaEventProducer> logger,
            IOptions<KafkaSettings> kafkaSettings,
            IOptions<KafkaProducerSettings> producerSettings)
        {
            _logger = logger;

            var settings = producerSettings.Value;
            var kafka = kafkaSettings.Value;

            var bootstrapServer = kafka.Mode == "local"
                ? kafka.BootstrapServerLocal
                : kafka.BootstrapServerDocker;

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = bootstrapServer,
                Acks = settings.Acks,
                EnableIdempotence = settings.EnableIdempotence,
                MessageTimeoutMs = settings.MessageTimeoutMs,
            };

            _producer = new ProducerBuilder<string, string>(producerConfig).Build();
        }

        public Task<bool> ProduceDlqEventAsync(
            string topic,
            DeadLetterQueueMessage deadLetterQueueMessage,
            CancellationToken stoppingToken)
        {
            var key = deadLetterQueueMessage.Key;
            var payload = JsonSerializer.Serialize(deadLetterQueueMessage);
            return ProduceInternalAsync(topic, key, payload, stoppingToken);
        }

        public Task<bool> ProduceEventAsync(
            string topic,
            string key,
            string payload,
            CancellationToken stoppingToken)
        {
            return ProduceInternalAsync(topic, key, payload, stoppingToken);
        }

        // Opt 2: Shared internal method — eliminates duplicated try/catch blocks
        private async Task<bool> ProduceInternalAsync(
            string topic,
            string key,
            string payload,
            CancellationToken stoppingToken)
        {
            // Opt 1: Check on _producer (was incorrectly named _dlqProducer)
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                await _producer.ProduceAsync(
                    topic,
                    new Message<string, string> { Key = key, Value = payload },
                    stoppingToken);

                return true;
            }
            catch (OperationCanceledException)
            {
                // Opt 5: Log message now correctly says "topic" not "DLQ topic"
                _logger.LogWarning(
                    "Operation cancelled while producing message to topic {Topic}.", topic);
                return false;
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(ex,
                    "Failed to produce message to topic {Topic}. Error: {Error}",
                    topic, ex.Error.Reason);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error while producing message to topic {Topic}. Error: {Error}",
                    topic, ex.Message);
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            // Opt 4: Set _disposed before disposal to prevent re-entry from DisposeAsync
            _disposed = true;

            _producer.Flush(TimeSpan.FromSeconds(10));
            _producer.Dispose();

            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            // Opt 4: Set _disposed before disposal to prevent re-entry from Dispose
            _disposed = true;

            // Opt 3: Flush is synchronous in Confluent's client — Task.Run is the correct
            // workaround since no native async flush exists. Documented here for clarity.
            await Task.Run(() => _producer.Flush(TimeSpan.FromSeconds(10)));
            _producer.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}