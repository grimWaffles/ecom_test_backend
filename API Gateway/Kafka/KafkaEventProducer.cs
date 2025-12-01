using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace API_Gateway.Helpers
{
    public interface IKafkaEventProducer
    {
        Task<KafkaProducerResult> ProduceEventUsingManualControlsAsync(string topic, string key, string payload, int? partition = null, CancellationToken token = default);
        Task<KafkaProducerResult> ProduceEventAsync(string topic, string key, string payload, CancellationToken token = default);
    }

    public class KafkaEventProducer : IKafkaEventProducer, IAsyncDisposable
    {
        private readonly KafkaProducerSettings _kafkaProducerSettings;

        private readonly IProducer<string, string> _producer;

        //For Manual Partitioning support
        private int _partitionCounter = -1;

        public KafkaEventProducer(IOptions<KafkaProducerSettings> options)
        {
            _kafkaProducerSettings = options.Value;

            #region KafkaSettings Validation
            if (_kafkaProducerSettings.BootstrapServer.IsNullOrEmpty()
                || _kafkaProducerSettings.MessageTimeoutMs <= 0
                || _kafkaProducerSettings.RetryAfterDelayMs <= 0
                || _kafkaProducerSettings.MaxNoOfRetries <= 0
                || _kafkaProducerSettings.TotalPartitions <= 0)
            {
                throw new Exception("Kafka settings are not configured correctly.");
            }
            #endregion

            ProducerConfig producerConfig = new ProducerConfig
            {
                BootstrapServers = _kafkaProducerSettings.BootstrapServer,
                EnableIdempotence = _kafkaProducerSettings.EnableIdempotence,   // safe retries, no duplicates
                MessageTimeoutMs = _kafkaProducerSettings.MessageTimeoutMs,
                Acks = _kafkaProducerSettings.Acks,    // stronger delivery guarantees
                MessageSendMaxRetries = _kafkaProducerSettings.MaxNoOfRetries
            };

            _producer = new ProducerBuilder<string, string>(producerConfig).Build();
        }

        public async ValueTask DisposeAsync()
        {
            _producer.Flush(TimeSpan.FromSeconds(10));
            _producer.Dispose();
        }

        #region using Kafka's Auto mechanisms for message producing
        /*
            This version relies on kafka to find:
            1) the appropriate partition
            2) if, when and how many times it will retry a message
            3) the delay between retries
         */
        public async Task<KafkaProducerResult> ProduceEventAsync(string topic, string key, string payload, CancellationToken token = default)
        {
            //Validate the inputs
            if (topic.IsNullOrEmpty() || key.IsNullOrEmpty() || payload.IsNullOrEmpty())
            {
                return new KafkaProducerResult()
                {
                    Status = false,
                    ErrorMessage = "Topic and message are incorrect"
                };
            }

            try
            {
                Message<string, string> messageToSend = new Message<string, string>
                { Key = key, Value = payload };

                DeliveryResult<string, string> result = await _producer.ProduceAsync(topic, messageToSend, token);

                return new KafkaProducerResult() { 
                    Status = true, 
                    ErrorMessage = $"Message delivered.{result.Status}", 
                    PartitionNumber = result.Partition.Value, 
                    Topic = result.Topic,
                    Offset = result.TopicPartitionOffset.Offset.Value 
                };
            }

            catch (OperationCanceledException)
            {
                return new KafkaProducerResult
                {
                    Status = false,
                    ErrorMessage = "Operation cancelled",
                    Topic = topic
                };
            }

            catch (ProduceException<string, string> ex)
            {
                return new KafkaProducerResult
                {
                    Status = false,
                    ErrorMessage = $"Fatal Kafka error: {ex.Error.Reason}",
                    Topic = topic
                };
            }

            catch (Exception ex)
            {
                // unexpected system-level error (IO, serialization, etc.)
                return new KafkaProducerResult
                {
                    Status = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                    Topic = topic
                };
            }
        }

        #endregion

        #region using manual controls for message producing
        public async Task<KafkaProducerResult> ProduceEventUsingManualControlsAsync(string topic, string key, string payload, int? partition = null, CancellationToken token = default)
        {
            //Validate the inputs
            if (topic.IsNullOrEmpty() || key.IsNullOrEmpty() || payload.IsNullOrEmpty())
            {
                return new KafkaProducerResult()
                {
                    Status = false,
                    ErrorMessage = "Topic and message are incorrect"
                };
            }

            int attemptNo = 0;
            string errorMessage = "";

            int selectedPartition = partition ?? GetNextPartition();

            while (attemptNo < Math.Max(1, _kafkaProducerSettings.MaxNoOfRetries))
            {
                try
                {
                    Message<string, string> messageToSend = new Message<string, string>
                    { Key = key, Value = payload };

                    TopicPartition target = new TopicPartition(topic, new Partition(selectedPartition));

                    await _producer.ProduceAsync(target, messageToSend, token);

                    return new KafkaProducerResult() { Status = true, ErrorMessage = "Successfully produced message" };
                }

                catch (OperationCanceledException)
                {
                    return new KafkaProducerResult
                    {
                        Status = false,
                        ErrorMessage = "Operation cancelled",
                        PartitionNumber = selectedPartition,
                        Topic = topic
                    };
                }

                catch (ProduceException<string, string> ex)
                {
                    var code = ex.Error.Code;

                    if (IsFatalKafkaError(code))
                    {
                        return new KafkaProducerResult
                        {
                            Status = false,
                            ErrorMessage = $"Fatal Kafka error: {ex.Error.Reason}",
                            PartitionNumber = selectedPartition,
                            Topic = topic
                        };
                    }

                    // Only retriable errors continue the loop
                    errorMessage = ex.Error.Reason;
                }

                catch (Exception ex)
                {
                    // unexpected system-level error (IO, serialization, etc.)
                    return new KafkaProducerResult
                    {
                        Status = false,
                        ErrorMessage = $"Unexpected error: {ex.Message}",
                        PartitionNumber = selectedPartition,
                        Topic = topic
                    };
                }

                attemptNo++;

                try
                {
                    await Task.Delay(_kafkaProducerSettings.RetryAfterDelayMs * attemptNo, token);
                }
                catch (OperationCanceledException e)
                {
                    break;
                }
            }

            return new KafkaProducerResult()
            {
                Status = false,
                ErrorMessage = errorMessage,
                PartitionNumber = selectedPartition,
                Topic = topic
            };
        }

        private int GetNextPartition()
        {
            int total = Math.Max(1, _kafkaProducerSettings.TotalPartitions);
            int nextPartition = Interlocked.Increment(ref _partitionCounter); //Thread-safe incrementing
            int finalPartition = nextPartition % total;

            return finalPartition < 0 ? finalPartition + total : finalPartition;
        }

        private static bool IsRetriableKafkaError(ErrorCode code)
        {
            return code == ErrorCode.BrokerNotAvailable
                || code == ErrorCode.LeaderNotAvailable
                || code == ErrorCode.RequestTimedOut
                || code == ErrorCode.NotEnoughReplicas
                || code == ErrorCode.NotEnoughReplicasAfterAppend
                || code == ErrorCode.NetworkException
                || code == ErrorCode.ClusterAuthorizationFailed;
        }

        private static bool IsFatalKafkaError(ErrorCode code)
        {
            // Anything not retriable is treated as fatal
            return !IsRetriableKafkaError(code);
        }

        #endregion
    }
}
