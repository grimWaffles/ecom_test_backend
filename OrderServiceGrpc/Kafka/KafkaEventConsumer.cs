
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Repository;
using OrderServiceGrpc.Services;
using System.Collections.Concurrent;
using System.Text.Json;

namespace OrderServiceGrpc.Kafka
{
    public class KafkaEventConsumer : BackgroundService
    {
        //ILogger
        private readonly ILogger<KafkaEventConsumer> _logger;

        //IServiceProvider for the order repo service (A scoped service DI'd into a singleton)
        private readonly IServiceProvider _serviceProvider;

        // Local commit tracking (topic-partition -> offset to commit)
        private readonly ConcurrentDictionary<TopicPartition, Offset> _processedOffsets = new();

        //Kafka Consumer Settings
        private readonly KafkaConsumerSettings _consumerSettings;

        // Kafka consumer configuration
        private ConsumerConfig _consumerConfig;

        // Kafka producer configuration for DLQ
        private ProducerConfig _dlqProducerConfig;

        // Producer used to send messages to DLQ topics
        private IProducer<string, string> _dlqProducer;

        // Kafka consumer for main topics
        private IConsumer<string, string> _consumer;

        public KafkaEventConsumer(ILogger<KafkaEventConsumer> logger, IServiceProvider serviceProvider, IOptions<KafkaConsumerSettings> kafkaConsumerSettings)
        {
            _logger = logger;

            _serviceProvider = serviceProvider;

            _logger.LogInformation("Kafka consumer constructor started...");

            // Load Kafka bootstrap server, topics, and DLQ topics from configuration
            _consumerSettings = kafkaConsumerSettings.Value;

            _logger.LogInformation("Kafka consumer settings loaded...");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ConfigureKafkaSettings();

            _ = Task.Run(() => StartKafkaConsumer(stoppingToken), stoppingToken);
        }

        public override async Task<Task> StopAsync(CancellationToken cancellationToken)
        {
            await CommitOffsets(cancellationToken);

            await CloseAndDisposeConsumerAndProducer();

            return base.StopAsync(cancellationToken);
        }

        private async Task ConfigureKafkaSettings()
        {
            try
            {
                // Validate required config values early
                if (string.IsNullOrWhiteSpace(_consumerSettings.BootstrapServer))
                    throw new ArgumentException("Kafka BootstrapServer is missing from configuration.");

                if (string.IsNullOrWhiteSpace(_consumerSettings.GroupId))
                    throw new ArgumentException("Kafka GroupId is missing from configuration.");

                if (_consumerSettings.TopicsToConsume.Length == 0)
                    throw new ArgumentException("No Kafka topics specified in configuration.");

                if (_consumerSettings.DlqTopics.Length == 0)
                    throw new ArgumentException("No Kafka DLQ topics specified in configuration.");

                // Consumer configuration
                _consumerConfig = new ConsumerConfig()
                {
                    BootstrapServers = _consumerSettings.BootstrapServer,
                    GroupId = _consumerSettings.GroupId,
                    AutoOffsetReset = _consumerSettings.AutoOffsetReset, // Start from beginning if no committed offsets
                    EnableAutoCommit = _consumerSettings.EnableAutoCommit,                   // We'll commit manually after successful processing
                    EnableAutoOffsetStore = _consumerSettings.EnableAutoOffsetStore,              // We'll explicitly store offsets after processing
                    AutoCommitIntervalMs = _consumerSettings.AutoCommitIntervalInMs
                };

                // Producer configuration for DLQ messages
                _dlqProducerConfig = new ProducerConfig()
                {
                    BootstrapServers = _consumerSettings.BootstrapServer,
                    Acks = _consumerSettings.DlqAcks,            // Wait for all replicas to acknowledge
                    EnableIdempotence = _consumerSettings.DlqIdempotence,  // Ensure no duplicate DLQ messages
                    MessageTimeoutMs = _consumerSettings.DlqMessageTimeoutMs,
                };
                // Initialize DLQ producer
                _dlqProducer = new ProducerBuilder<string, string>(_dlqProducerConfig).Build();

                // Initialize main topic consumer and subscribe
                _consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();

                _consumer.Subscribe(_consumerSettings.TopicsToConsume);

                _logger.LogInformation("Initialized consumer service successfully");
            }

            catch (Exception e)
            {
                _logger.LogInformation($"Failed to subscribe to topics. Exception: {e.Message}", e.StackTrace);
                await CloseAndDisposeConsumerAndProducer();
                return;
            }
        }

        private async Task StartKafkaConsumer(CancellationToken stoppingToken)
        {
            //Commit Interval
            DateTime lastCommitTime = DateTime.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    bool processedMessage = false;

                    ConsumeResult<string, string> result = _consumer.Consume(TimeSpan.FromMilliseconds(100));

                    //If result is null or empty
                    if (result == null)
                    {
                        await Task.Delay(100, stoppingToken); // prevent CPU spin
                        continue;
                    }

                    ProcessorResponseModel repoResponse = new ProcessorResponseModel();

                    //Validate message topic
                    bool topicExists = Array.Exists(_consumerSettings.TopicsToConsume, x => x == result.Topic);

                    //Implementing Max Retries if valid topic
                    if (topicExists)
                    {
                        processedMessage = await ProcessMessageResult(result, stoppingToken);
                    }

                    //DLQ Processing
                    if (!processedMessage || !topicExists)
                    {
                        await SendToDlq(result, topicExists, repoResponse, stoppingToken);
                    }

                    //Add to processed messages
                    _processedOffsets[result.TopicPartition] = result.Offset + 1;

                    //check whether it is time to commit the offsets
                    if (DateTime.UtcNow - lastCommitTime >= TimeSpan.FromMilliseconds(_consumerSettings.MaxDelayBetweenCommitsInMs) || _processedOffsets.Count() >= _consumerSettings.ConsumerMessageBatchSize)
                    {
                        await CommitOffsets(stoppingToken);
                        lastCommitTime = DateTime.UtcNow;
                    }
                }
                catch (ConsumeException cex)
                {
                    await Task.Delay(_consumerSettings.MaxDelayBetweenCommitsInMs, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation($"KAFKA ORDER CONSUMER: Consumer has been cancelled by user");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"KAFKA ORDER CONSUMER Fatal Error: {ex.Message}");
                }
            }
        }

        private async Task SendToDlq(ConsumeResult<string, string> result, bool topicExists, ProcessorResponseModel repoResponse, CancellationToken stoppingToken)
        {
            string dlqTopic = ""; bool produceDlq = false;

            //Check if DLQ topic exists for that topic, if we get an empty topic we know what to do
            if (topicExists)
            {
                dlqTopic = _consumerSettings.DlqTopics.Where(x => x == result.Topic + "-dlq").FirstOrDefault() ?? "";
                dlqTopic = dlqTopic == "" ? $"No DLQ topic found for topic:{result.Topic}" : dlqTopic;

                produceDlq = dlqTopic == "" ? false : true;
            }
            else
            {
                dlqTopic = $"Invalid Topic Found:{result.Topic}";
                produceDlq = false;
            }

            // If processing fails, prepare a DLQ message with metadata
            var dlqMessage = new
            {
                OriginalTopic = result.Topic,
                OriginalMessage = result.Message.Value,
                Key = result.Message.Key,
                Exception = repoResponse.Message,
                StackTrace = repoResponse.StackTrace,
                TimeStamp = DateTime.Now,
                Partition = result.Partition,
                Offset = result.Offset,
            };

            if (produceDlq)
            {
                // Produce failed message to DLQ
                try
                {
                    await _dlqProducer.ProduceAsync(dlqTopic, new Message<string, string>
                    {
                        Key = result.Message.Key.ToString(),
                        Value = JsonSerializer.Serialize(dlqMessage)
                    }, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"KAFKA ORDER CONSUMER EXCEPTION: Failed to produce DLQ message for {result.Topic}. ErrorMessage: {ex.Message}");
                }
            }
            else
            {
                _logger.LogInformation($"Failed to process DLQ Message: {result.Topic}. Sending to Catch-All...");

                // Produce failed message to CATCH-ALL-DLQ
                try
                {
                    await _dlqProducer.ProduceAsync(_consumerSettings.CatchAllDlqTopic, new Message<string, string>
                    {
                        Key = result.Message.Key.ToString(),
                        Value = JsonSerializer.Serialize(dlqMessage)
                    }, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"KAFKA ORDER CONSUMER EXCEPTION: Failed to produce DLQ message for {result.Topic}. ErrorMessage: {ex.Message}");
                }
            }
        }

        private async Task CommitOffsets(CancellationToken token)
        {
            var topicPartitionOffsets = _processedOffsets
            .Select(kv => new TopicPartitionOffset(kv.Key, kv.Value))
            .ToList();

            if (!topicPartitionOffsets.Any()) return;

            for (int attemptNo = 1; attemptNo < _consumerSettings.MaxConsumerRetries; attemptNo++)
            {
                _logger.LogInformation($"Attemp#{attemptNo + 1} to commit result...");

                try
                {
                    _consumer.Commit(topicPartitionOffsets);

                    // remove committed offsets from tracking dictionary
                    foreach (var tpo in topicPartitionOffsets)
                    {
                        _processedOffsets.TryRemove(tpo.TopicPartition, out _);
                    }

                    _logger.LogInformation($"Committed {topicPartitionOffsets.Count} offset(s) successfully.");

                    break;
                }
                catch (KafkaException kex)
                {
                    _logger.LogWarning($"Attempt {attemptNo + 1}: Failed to commit offsets: {kex.Message}");

                    if (attemptNo < _consumerSettings.MaxConsumerRetries - 1)
                    {
                        int delayMs = GetExponentialDelay(attemptNo);
                        _logger.LogInformation($"Retrying commit in {delayMs}ms...");

                        await Task.Delay(delayMs, token);
                    }
                    else
                    {
                        _logger.LogInformation($"Failed to commit offsets after {_consumerSettings.MaxConsumerRetries} attempts.");
                    }
                }
            }
        }

        private async Task CloseAndDisposeConsumerAndProducer()
        {
            try
            {
                await CommitOffsets(CancellationToken.None);
                _consumer.Close();
                _consumer.Dispose();
                _dlqProducer.Flush();
                _dlqProducer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogInformation("KAFKA ORDER CONSUMER: Error closing and shutting down consumer and dlqProducer");
            }
        }

        private async Task<bool> ProcessMessageResult(ConsumeResult<string, string> result, CancellationToken stoppingToken)
        {
            for (int attemptNo = 0; attemptNo < _consumerSettings.MaxConsumerRetries; attemptNo++)
            {
                try
                {
                    ProcessorResponseModel repoResponse = await ProcessOrderEvent(result, stoppingToken);

                    if (repoResponse.Status == true)
                    {
                        return true;
                    }

                    _logger.LogInformation($"Error: Processing failed for topic '{result.Topic}'. Retrying...");

                    await Task.Delay(GetExponentialDelay(attemptNo), stoppingToken);
                }
                catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation($"KAFKA ORDER CONSUMER: Consumer has been cancelled by user");
                    throw;
                }
                catch (Exception ex)
                {
                    if (attemptNo < _consumerSettings.MaxConsumerRetries)
                    {
                        await Task.Delay(GetExponentialDelay(attemptNo), stoppingToken);
                    }
                }
            }
            return false;
        }

        private async Task<ProcessorResponseModel> ProcessOrderEvent(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            try
            {
                OrderModel model = JsonSerializer.Deserialize<OrderModel>(result.Message.Value);

                using (var scope = _serviceProvider.CreateScope())
                {
                    IOrderProcessorService processorService = scope.ServiceProvider.GetRequiredService<IOrderProcessorService>();

                    ProcessorResponseModel repoResponse = (result.Topic) switch
                    {
                        "order-create" => await processorService.CreateOrder(model, 1),
                        "order-update" => await processorService.UpdateOrder(model, 1),
                        "order-delete" => await processorService.DeleteOrder(model.Id, 1),
                        _ => new ProcessorResponseModel()
                        {
                            Status = false,
                            Message = $"Error: Invalid topic provided in message={result.Topic}"
                        }
                    };

                    return repoResponse;
                }
            }
            catch (Exception e)
            {
                return new ProcessorResponseModel()
                {
                    Status = false,
                    Message = $"Error: Invalid topic provided in message:{result.Topic}. STACKTRACE: {e.StackTrace}"
                };
            }
        }

        private int GetExponentialDelay(int attemptNo)
        {
            return (int)Math.Pow(2, attemptNo) * 100;
        }
    }
}
