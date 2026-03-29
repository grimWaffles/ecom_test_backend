using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.ConfigModels;
using System.Text.Json;

namespace OrderServiceGrpc.Kafka
{
    public class KafkaEventProducer : IDisposable, IAsyncDisposable
    {
        private readonly ILogger<KafkaEventProducer> _logger; 
        private readonly ProducerConfig _dlqProducerConfig;
        private readonly IProducer<string, string> _dlqProducer;
        private readonly KafkaProducerSettings _producerSettings;
        private bool _disposed;

        public KafkaEventProducer(ILogger<KafkaEventProducer> logger, string kafkaBootstrapServer, IOptions<KafkaProducerSettings> producerSettings)
        {
            _logger = logger;

            _producerSettings = producerSettings.Value;

            _dlqProducerConfig = new ProducerConfig
            {
                BootstrapServers = kafkaBootstrapServer,
                Acks = _producerSettings.Acks,
                EnableIdempotence = _producerSettings.EnableIdempotence,
                MessageTimeoutMs = _producerSettings.MessageTimeoutMs,
            };

            _dlqProducer = new ProducerBuilder<string, string>(_dlqProducerConfig).Build();
        }

        public async Task<bool> ProduceDlqEventAsync(string topic, DeadLetterQueueMessage deadLetterQueueMessage,  CancellationToken stoppingToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this); 

            try
            {
                await _dlqProducer.ProduceAsync(
                    topic,
                    new Message<string, string>
                    {
                        Key = deadLetterQueueMessage.Key,
                        Value = JsonSerializer.Serialize(deadLetterQueueMessage)
                    },
                    stoppingToken);

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Operation cancelled while producing message to DLQ topic {Topic}.", topic);
                return false;
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(ex,
                    "Failed to produce message to DLQ topic {Topic}. Error: {Error}",
                    topic, ex.Error.Reason);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error while producing message to DLQ topic {Topic}. Error: {Error}",
                    topic, ex.Message);
                return false;
            }
        }

        public async Task<bool> ProduceEventAsync(string topic, string key, string payload, CancellationToken stoppingToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                await _dlqProducer.ProduceAsync(
                    topic,
                    new Message<string, string>
                    {
                        Key = key,
                        Value = payload
                    },
                    stoppingToken);

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Operation cancelled while producing message to DLQ topic {Topic}.", topic);
                return false;
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(ex,
                    "Failed to produce message to DLQ topic {Topic}. Error: {Error}",
                    topic, ex.Error.Reason);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error while producing message to DLQ topic {Topic}. Error: {Error}",
                    topic, ex.Message);
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _dlqProducer.Flush(TimeSpan.FromSeconds(10)); // drain any buffered messages
            _dlqProducer.Dispose();
            _disposed = true;

            GC.SuppressFinalize(this);
        }


        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            await Task.Run(() => _dlqProducer.Flush(TimeSpan.FromSeconds(10)));
            _dlqProducer.Dispose();
            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}
