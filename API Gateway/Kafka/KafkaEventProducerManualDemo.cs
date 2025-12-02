using Confluent.Kafka;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API_Gateway.Helpers
{
    public class KafkaSettings
    {
        public string BootstrapServers { get; set; } = "";
        public int TotalPartitions { get; set; } = 6;
        public Acks Acks { get; set; } = Acks.All;
        public bool EnableIdempotence { get; set; } = true;
        public int MessageTimeoutMs { get; set; } = 5000;
        public int MaxRetries { get; set; } = 3;
        public int RetryBaseDelayMs { get; set; } = 200; // exponential base

        // Optional security (SASL/SSL)
        public string SecurityProtocol { get; set; } = null!; // e.g. "SaslSsl"
        public string SaslMechanism { get; set; } = null!;   // e.g. "Plain"
        public string SaslUsername { get; set; } = null!;
        public string SaslPassword { get; set; } = null!;
    }

    public record ProduceResult(bool Success, string? ErrorMessage = null, TopicPartitionOffset? Offset = null);

    public interface IKafkaEventProducer_v2 : IAsyncDisposable
    {
        Task<ProduceResult> ProduceEventAsync(string topic, string key, string payload, int? partition = null, CancellationToken token = default);
    }

    public class KafkaEventProducerManualDemo : IKafkaEventProducer_v2
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaEventProducer> _logger;
        private readonly KafkaSettings _settings;

        // For manual partitioning (thread-safe counter)
        private int _partitionCounter = -1; // start at -1 so first Interlocked.Increment becomes 0

        public KafkaEventProducerManualDemo(IOptions<KafkaSettings> settings, ILogger<KafkaEventProducer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

            var config = new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                Acks = _settings.Acks,
                EnableIdempotence = _settings.EnableIdempotence,
                MessageTimeoutMs = _settings.MessageTimeoutMs
            };

            // Optional security configuration
            if (!string.IsNullOrWhiteSpace(_settings.SecurityProtocol))
            {
                // These string values come from Confluent.Kafka enums; user should set values that map to the library's expectations.
                config.SecurityProtocol = Enum.TryParse<SecurityProtocol>(_settings.SecurityProtocol, out var sec) ? sec : config.SecurityProtocol;
            }

            if (!string.IsNullOrWhiteSpace(_settings.SaslMechanism))
            {
                config.SaslMechanism = Enum.TryParse<SaslMechanism>(_settings.SaslMechanism, out var mech) ? mech : config.SaslMechanism;
            }

            if (!string.IsNullOrWhiteSpace(_settings.SaslUsername)) config.SaslUsername = _settings.SaslUsername;
            if (!string.IsNullOrWhiteSpace(_settings.SaslPassword)) config.SaslPassword = _settings.SaslPassword;

            // Create producer with default serializers
            _producer = new ProducerBuilder<string, string>(config)
                .SetErrorHandler((_, e) => _logger.LogError("Kafka producer error: {Reason}", e.Reason))
                .SetLogHandler((_, m) => _logger.LogDebug("Kafka log: {Name} - {Message}", m.Name, m.Message))
                .Build();

            _logger.LogInformation("KafkaEventProducer initialized. Brokers: {Brokers}", _settings.BootstrapServers);
        }

        private int GetNextPartition()
        {
            // Interlocked to ensure thread-safety and proper rolling
            return Math.Abs((Interlocked.Increment(ref _partitionCounter)) % Math.Max(1, _settings.TotalPartitions));
        }

        public async Task<ProduceResult> ProduceEventAsync(string topic, string key, string payload, int? partition = null, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(topic)) return new ProduceResult(false, "Topic cannot be empty");

            // Basic validation
            key ??= string.Empty;
            payload ??= string.Empty;

            var message = new Message<string, string> { Key = key, Value = payload };

            int selectedPartition = partition ?? GetNextPartition();
            var tp = new TopicPartition(topic, new Partition(selectedPartition));

            int attempt = 0;
            Exception? lastEx = null;

            while (attempt <= Math.Max(0, _settings.MaxRetries))
            {
                attempt++;
                try
                {
                    var result = await _producer.ProduceAsync(tp, message, token).ConfigureAwait(false);

                    return new ProduceResult(true, null, result.TopicPartitionOffset);
                }
                catch (ProduceException<string, string> pe)
                {
                    lastEx = pe;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    lastEx = ex;
                }

                if (attempt > _settings.MaxRetries) break;

                // Exponential backoff with jitter
                var delay = ComputeBackoffDelayMs(_settings.RetryBaseDelayMs, attempt);
                try 
                { 
                    await Task.Delay(delay, token).ConfigureAwait(false);
                } 
                catch (OperationCanceledException) 
                { 
                    break; 
                }
            }

            var errorMsg = lastEx?.Message ?? "Unknown error while producing message";
            return new ProduceResult(false, errorMsg);
        }

        private static int ComputeBackoffDelayMs(int baseMs, int attempt)
        {
            // base * 2^(attempt-1) + jitter up to base
            var exponential = baseMs * (1 << Math.Max(0, attempt - 1));
            var jitter = new Random().Next(0, baseMs);
            // cap delay to a reasonable value (e.g., 30s)
            return Math.Min(exponential + jitter, 30_000);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _logger.LogInformation("Flushing Kafka producer before dispose...");
                // Flush outstanding messages with timeout
                _producer.Flush(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while flushing producer");
            }
            finally
            {
                _producer.Dispose();
                _logger.LogInformation("Kafka producer disposed");
            }

            await Task.CompletedTask;
        }
    }
}

