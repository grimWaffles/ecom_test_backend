using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Helpers.cs;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.ConfigModels;
using OrderServiceGrpc.Models.Configs;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Services;
using System.Collections.Concurrent;
using System.Text.Json;

namespace OrderServiceGrpc.Kafka
{
    public class TransactionEventConsumer : BackgroundService
    {
        //ILogger
        private readonly ILogger<OrderEventConsumer> _logger;

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

        //Global Kafka Settings
        private KafkaSettings _kafkaSettings;

        private TransactionEventConsumerSettings _transactionEventConsumerSettings;

        public TransactionEventConsumer(ILogger<OrderEventConsumer> logger, IServiceProvider serviceProvider, IOptions<KafkaSettings> kafkaSettings, IOptions<KafkaConsumerSettings> kafkaConsumerSettings, IOptions<TransactionEventConsumerSettings> tecSettings)
        {
            _logger = logger;

            _serviceProvider = serviceProvider;

            _logger.LogInformation("KAFKA TRX CONSUMER: Kafka consumer constructor started...");

            // Load Kafka bootstrap server, topics, and DLQ topics from configuration
            _consumerSettings = kafkaConsumerSettings.Value;

            _logger.LogInformation("KAFKA TRX CONSUMER: Kafka consumer settings loaded...");

            _kafkaSettings = kafkaSettings.Value;
            _transactionEventConsumerSettings = tecSettings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ConfigureKafkaSettings();

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
            //Checks for the kafka server are done from the global kafka settings
            try
            {
                // Validate required config values early
                if (string.IsNullOrWhiteSpace(_kafkaSettings.BootstrapServerDocker))
                    throw new ArgumentException("Kafka BootstrapServer for docker is missing from configuration.");

                if (string.IsNullOrWhiteSpace(_kafkaSettings.BootstrapServerLocal))
                    throw new ArgumentException("Kafka BootstrapServer for dev is missing from configuration.");

                if (string.IsNullOrWhiteSpace(_consumerSettings.TransactionGroupId))
                    throw new ArgumentException("Kafka GroupId is missing from configuration.");

                if (_transactionEventConsumerSettings.TopicsToConsume.Length == 0)
                    throw new ArgumentException("No Kafka topics specified in configuration.");

                if (_transactionEventConsumerSettings.TopicsToProduce.Length == 0)
                    throw new ArgumentException("No Kafka DLQ topics specified in configuration.");

                string kafkaBootstrapServer = _kafkaSettings.Mode == "local" ? _kafkaSettings.BootstrapServerLocal : _kafkaSettings.BootstrapServerDocker;

                // Consumer configuration
                _consumerConfig = new ConsumerConfig()
                {
                    BootstrapServers = kafkaBootstrapServer,
                    GroupId = _consumerSettings.TransactionGroupId,
                    AutoOffsetReset = _consumerSettings.AutoOffsetReset, // Start from beginning if no committed offsets
                    EnableAutoCommit = _consumerSettings.EnableAutoCommit,                   // We'll commit manually after successful processing
                    EnableAutoOffsetStore = _consumerSettings.EnableAutoOffsetStore,              // We'll explicitly store offsets after processing
                    AutoCommitIntervalMs = _consumerSettings.AutoCommitIntervalInMs
                };

                // Producer configuration for DLQ messages
                _dlqProducerConfig = new ProducerConfig()
                {
                    BootstrapServers = kafkaBootstrapServer,
                    Acks = _consumerSettings.DlqAcks,            // Wait for all replicas to acknowledge
                    EnableIdempotence = _consumerSettings.DlqIdempotence,  // Ensure no duplicate DLQ messages
                    MessageTimeoutMs = _consumerSettings.DlqMessageTimeoutMs,
                };
                // Initialize DLQ producer
                _dlqProducer = new ProducerBuilder<string, string>(_dlqProducerConfig).Build();

                // Initialize main topic consumer and subscribe
                _consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();

                _consumer.Subscribe(_transactionEventConsumerSettings.TopicsToConsume);

                _logger.LogInformation("KAFKA TRX CONSUMER: Initialized TRX consumer service successfully");
            }

            catch (Exception e)
            {
                _logger.LogInformation($"KAFKA TRX CONSUMER: Failed to subscribe to topics. Exception: {e.Message}", e.StackTrace);
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

                    ConsumerResponseModel repoResponse = new ConsumerResponseModel();

                    //Validate message topic
                    bool topicExists = Array.Exists(_transactionEventConsumerSettings.TopicsToConsume, x => x == result.Topic);

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
                    _logger.LogInformation($"KAFKA TRX CONSUMER: Consumer has been cancelled by user");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"KAFKA TRX CONSUMER Fatal Error: {ex.Message}");
                }
            }
        }

        private async Task SendToDlq(ConsumeResult<string, string> result, bool topicExists, ConsumerResponseModel repoResponse, CancellationToken stoppingToken)
        {
            string dlqTopic = ""; bool produceDlq = false;

            //Check if DLQ topic exists for that topic, if we get an empty topic we know what to do
            if (topicExists)
            {
                dlqTopic = _transactionEventConsumerSettings.TopicsToProduce.Where(x => x == result.Topic + "-dlq").FirstOrDefault() ?? "";
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
                    _logger.LogInformation($"KAFKA TRX CONSUMER EXCEPTION: Failed to produce DLQ message for {result.Topic}. ErrorMessage: {ex.Message}");
                }
            }
            else
            {
                _logger.LogInformation($"KAFKA TRX CONSUMER: Failed to process DLQ Message: {result.Topic}. Sending to Catch-All...");

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
                    _logger.LogInformation($"KAFKA TRX CONSUMER EXCEPTION: Failed to produce DLQ message for {result.Topic}. ErrorMessage: {ex.Message}");
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
                _logger.LogInformation($"KAFKA TRX CONSUMER: Attemp#{attemptNo + 1} to commit result...");

                try
                {
                    _consumer.Commit(topicPartitionOffsets);

                    // remove committed offsets from tracking dictionary
                    foreach (var tpo in topicPartitionOffsets)
                    {
                        _processedOffsets.TryRemove(tpo.TopicPartition, out _);
                    }

                    _logger.LogInformation($"KAFKA TRX CONSUMER: Committed {topicPartitionOffsets.Count} offset(s) successfully.");

                    break;
                }
                catch (KafkaException kex)
                {
                    _logger.LogWarning($"KAFKA TRX CONSUMER: Attempt {attemptNo + 1}: Failed to commit offsets: {kex.Message}");

                    if (attemptNo < _consumerSettings.MaxConsumerRetries - 1)
                    {
                        int delayMs = GetExponentialDelay(attemptNo);
                        _logger.LogInformation($" KAFKA TRX CONSUMER:Retrying commit in {delayMs}ms...");

                        await Task.Delay(delayMs, token);
                    }
                    else
                    {
                        _logger.LogInformation($"KAFKA TRX CONSUMER: Failed to commit offsets after {_consumerSettings.MaxConsumerRetries} attempts.");
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
                _logger.LogInformation("KAFKA TRX CONSUMER: Error closing and shutting down consumer and dlqProducer");
            }
        }

        private async Task<bool> ProcessMessageResult(ConsumeResult<string, string> result, CancellationToken stoppingToken)
        {
            for (int attemptNo = 0; attemptNo < _consumerSettings.MaxConsumerRetries; attemptNo++)
            {
                try
                {
                    ConsumerResponseModel repoResponse = await ProcessTransactionEvent(result, stoppingToken);

                    if (repoResponse.Status == true)
                    {
                        return true;
                    }

                    _logger.LogInformation($"KAFKA TRX CONSUMER Error: Processing failed for topic '{result.Topic}'. Retrying...");

                    await Task.Delay(GetExponentialDelay(attemptNo), stoppingToken);
                }
                catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation($"KAFKA TRX CONSUMER: Consumer has been cancelled by user");
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

        private async Task<ConsumerResponseModel> ProcessTransactionEvent(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            try
            {
                CreateOrderRequestDto request = JsonSerializer.Deserialize<CreateOrderRequestDto>(result.Message.Value);

                if (request != null)
                {

                    CustomerTransactionDto trxDto = new CustomerTransactionDto()
                    {
                        UserId = request.UserId,
                        TransactionType = "PURCHASE",
                        Amount = (decimal) request.Order.NetAmount,
                        CreatedDate = DateTime.Now,
                        CreatedBy = request.Order.CreatedBy,
                        IsDeleted = request.Order.IsDeleted,
                        TransactionDate = DateTime.Now,
                        ModifiedDate = DateTime.Now,
                        ModifiedBy = request.Order.UserId,
                        TransactionKey = ""
                    };
                    int userId = trxDto.UserId;

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        ICustomerTransactionProcessorService processorService = scope.ServiceProvider.GetRequiredService<ICustomerTransactionProcessorService>();

                        if (result.Topic == "order-create")
                        {
                            int insertedId = await processorService.AddTransaction(trxDto, userId);

                            return new ConsumerResponseModel()
                            {
                                Status = insertedId > 0,
                                Message = insertedId > 0 ? $"KAFKA TRX CONSUMER: Transaction created with ID: {insertedId}" : "Failed to create transaction"
                            };
                        }
                        else if (result.Topic == "order-update")
                        {
                            bool updateResult = await processorService.UpdateTransaction(trxDto, userId);
                            return new ConsumerResponseModel()
                            {
                                Status = updateResult,
                                Message = updateResult ? $"KAFKA TRX CONSUMER: Transaction with ID: {trxDto.Id} updated successfully" : $"Failed to update transaction with ID: {trxDto.Id}"
                            };
                        }
                        else if (result.Topic == "order-delete")
                        {
                            bool deleteResult = await processorService.DeleteTransaction(trxDto, userId);
                            return new ConsumerResponseModel()
                            {
                                Status = deleteResult,
                                Message = deleteResult ? $"KAFKA TRX CONSUMER: Transaction with ID: {trxDto.Id} deleted successfully" : $"Failed to delete transaction with ID: {trxDto.Id}"
                            };
                        }
                        else
                        {
                            return new ConsumerResponseModel()
                            {
                                Status = false,
                                Message = $"KAFKA TRX CONSUMER: Error: Invalid topic provided in message={result.Topic}"
                            };
                        }
                    }
                }

                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = $"Error: Invalid message provided topic:{result.Topic}"
                };
            }
            catch (Exception e)
            {
                return new ConsumerResponseModel()
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
