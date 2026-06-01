using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Kafka.Producers;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.ConfigModels;
using OrderServiceGrpc.Models.Configs;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Services;
using System.Collections.Concurrent;
using System.Text.Json;

namespace OrderServiceGrpc.Kafka.Consumers
{
    public class TransactionEventConsumer : BackgroundService
    {
        //ILogger
        private readonly ILogger<TransactionEventConsumer> _logger;

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
        private IKafkaEventProducer _eventProducer;

        // Kafka consumer for main topics
        private IConsumer<string, string> _trxConsumer;

        //Global Kafka Settings
        private KafkaSettings _kafkaSettings;

        private TransactionEventConsumerSettings _transactionEventConsumerSettings;

        private readonly Dictionary<string, string> _compensationEventMap = new();

        public TransactionEventConsumer(ILogger<TransactionEventConsumer> logger, IKafkaEventProducer kafkaEventProducer, IServiceProvider serviceProvider, IOptions<KafkaSettings> kafkaSettings, IOptions<KafkaConsumerSettings> kafkaConsumerSettings, IOptions<TransactionEventConsumerSettings> tecSettings)
        {
            _logger = logger;

            _serviceProvider = serviceProvider;

            // Load Kafka bootstrap server, topics, and DLQ topics from configuration
            _consumerSettings = kafkaConsumerSettings.Value;
            _kafkaSettings = kafkaSettings.Value;
            _transactionEventConsumerSettings = tecSettings.Value;
            _eventProducer = kafkaEventProducer;

            foreach (string topic in _transactionEventConsumerSettings.TopicsToConsume)
            {
                string ct = topic.Replace("order", "transaction");
                ct = ct.Replace("success", "failed");

                if (!_transactionEventConsumerSettings.TopicsToProduce.Contains(ct))
                {
                    _logger.LogError("KAFKA TRX CONSUMER: Compensation topic {Topic} not found for consumed topic {ConsumedTopic}. Compensation events will not be produced for this topic.", ct, topic);
                }
                else
                {
                    _compensationEventMap[topic] = ct;
                }
            }

            _logger.LogInformation("KAFKA TRX CONSUMER: Constructor process complete");
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

                // Initialize main topic consumer and subscribe
                _trxConsumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();

                _trxConsumer.Subscribe(_transactionEventConsumerSettings.TopicsToConsume);

                _logger.LogInformation("KAFKA TRX CONSUMER: Initialized TRX consumer service successfully");
            }

            catch (Exception e)
            {
                _logger.LogCritical("KAFKA TRX CONSUMER: Failed to subscribe to topics. Exception: {Message}. StackTrace: {StackTrace}", e.Message, e.StackTrace);
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
                    ConsumerResponseModel processedMessage = new ConsumerResponseModel();

                    ConsumeResult<string, string> result = _trxConsumer.Consume(TimeSpan.FromMilliseconds(100));

                    //If result is null or empty
                    if (result == null)
                    {
                        await Task.Delay(100, stoppingToken); // prevent CPU spin
                        continue;
                    }

                    //Validate message topic
                    bool topicExists = Array.Exists(_transactionEventConsumerSettings.TopicsToConsume, x => x == result.Topic);

                    //Deserialize message value to OrderEventMessage
                    OrderEventMessage oem = JsonSerializer.Deserialize<OrderEventMessage>(result.Message.Value) ?? new OrderEventMessage();

                    if (oem.OrderId == 0)
                    {
                        _logger.LogError("KAFKA TRX CONSUMER: Failed to deserialize message from topic {Topic}. Message value: {MessageValue}", result.Topic, result.Message.Value);
                        continue; // Skip processing and do not commit offset, allowing for retry
                    }

                    //Implementing Max Retries if valid topic
                    if (topicExists && oem.OrderId > 0)
                    {
                        processedMessage = await ProcessMessageResult(result.Topic, oem, stoppingToken);
                    }

                    //TODO: Produce next event in saga processing

                    //Failure Processing
                    if (!processedMessage.Status || !topicExists)
                    {
                        await ProduceCompensatingEvent(result.Topic, oem, stoppingToken);
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
                    _logger.LogInformation("KAFKA TRX CONSUMER Fatal Error: {Message}", ex.Message);
                }
            }
        }
        private async Task<ConsumerResponseModel> ProcessMessageResult(string topic, OrderEventMessage result, CancellationToken stoppingToken)
        {
            ConsumerResponseModel repoResponse = new ConsumerResponseModel();

            for (int attemptNo = 0; attemptNo < _consumerSettings.MaxConsumerRetries; attemptNo++)
            {
                try
                {
                    repoResponse = await ProcessTransactionEvent(topic, result, stoppingToken);

                    if (repoResponse.Status == true)
                    {
                        return repoResponse;
                    }

                    _logger.LogError("KAFKA TRX CONSUMER Error: Processing failed for topic {Topic}. Retrying...", topic);

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
            return repoResponse;
        }

        private async Task<ConsumerResponseModel> ProcessTransactionEvent(string topic, OrderEventMessage request, CancellationToken cancellationToken)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    ICustomerTransactionService processorService = scope.ServiceProvider.GetRequiredService<ICustomerTransactionService>();

                    return topic switch
                    {
                        "order-create-success" => await HandleOrderCreate(processorService, request),
                        "order-update-success" => await HandleOrderUpdate(processorService, request),
                        "order-delete-success" => await HandleOrderDelete(processorService, request),
                        _ => new ConsumerResponseModel()
                        {
                            Status = false,
                            Message = $"KAFKA TRX CONSUMER: Error: Invalid topic provided in message-{topic}"
                        }
                    };
                }
            }
            catch (Exception e)
            {
                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = e.Message,
                    StackTrace = e.StackTrace ?? ""
                };
            }
        }

        private async Task<ConsumerResponseModel> HandleOrderUpdate(ICustomerTransactionService processorService, OrderEventMessage request)
        {
            CustomerTransactionDto trxDto = new CustomerTransactionDto()
            {
                UserId = request.UserId,
                TransactionType = "PURCHASE",
                Amount = (decimal)request.Amount,
                CreatedDate = DateTime.Now,
                CreatedBy = request.UserId,
                IsDeleted = false,
                TransactionDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                ModifiedBy = request.UserId,
                OrderId = request.OrderId,
                TransactionKey = ""
            };

            bool updateResult = await processorService.UpdateTransactionUsingOrderId(trxDto, request.UserId);

            return new ConsumerResponseModel()
            {
                Status = updateResult,
                Message = updateResult ? $"KAFKA TRX CONSUMER: Transaction with ID: {trxDto.Id} updated successfully" : $"Failed to update transaction with ID: {trxDto.Id}"
            };
        }

        private async Task<ConsumerResponseModel> HandleOrderCreate(ICustomerTransactionService processorService, OrderEventMessage request)
        {
            CustomerTransactionDto trxDto = new CustomerTransactionDto()
            {
                UserId = request.UserId,
                TransactionType = "PURCHASE",
                Amount = (decimal)request.Amount,
                CreatedDate = DateTime.Now,
                CreatedBy = request.UserId,
                IsDeleted = false,
                TransactionDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                ModifiedBy = request.UserId,
                TransactionKey = "",
                OrderId = request.OrderId
            };

            int insertedId = await processorService.AddTransaction(trxDto, request.UserId);

            return new ConsumerResponseModel()
            {
                Status = insertedId > 0,
                Message = insertedId > 0 ? $"KAFKA TRX CONSUMER: Transaction created with ID: {insertedId}" : "Failed to create transaction",
                TrxDto = trxDto,
            };
        }

        private async Task<ConsumerResponseModel> HandleOrderDelete(ICustomerTransactionService processorService, OrderEventMessage request)
        {
            CustomerTransactionDto trxDto = new CustomerTransactionDto()
            {
                UserId = request.UserId,
                OrderId = request.OrderId
            };

            bool deleteResult = await processorService.DeleteTransaction(trxDto, request.UserId);
            return new ConsumerResponseModel()
            {
                Status = deleteResult,
                Message = deleteResult ? $"KAFKA TRX CONSUMER: Transaction with ID: {trxDto.Id} deleted successfully" : $"Failed to delete transaction with ID: {trxDto.Id}",
                TrxDto = trxDto,
            };
        }

        private async Task<bool> ProduceCompensatingEvent(string topic, OrderEventMessage oem, CancellationToken stoppingToken)
        {
            _compensationEventMap.TryGetValue(topic, out var ct);

            try
            {
                if (string.IsNullOrEmpty(ct))
                {
                    _logger.LogError("KAFKA TRX CONSUMER: No compensation topic found for topic {Topic}. Failed to produce compensating event.", topic);
                    return false;
                }

                string payload = JsonSerializer.Serialize(oem);

                await _eventProducer.ProduceEventAsync(ct, oem.OrderId.ToString(), payload, stoppingToken);

                _logger.LogInformation("Produced compensating event for failed transaction operation. Topic: {topic}", ct);

                return true;
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("KAFKA TRX CONSUMER: Compensation event production cancelled by user for topic {ct}", ct);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("KAFKA TRX CONSUMER: Failed to produce compensating event for topic {Topic}. Error: {Message}", topic, ex.Message);
                return false;
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
                _logger.LogInformation("KAFKA TRX CONSUMER: Attemp#{AttemptNo} to commit result...", attemptNo + 1);

                try
                {
                    _trxConsumer.Commit(topicPartitionOffsets);

                    // remove committed offsets from tracking dictionary
                    foreach (var tpo in topicPartitionOffsets)
                    {
                        _processedOffsets.TryRemove(tpo.TopicPartition, out _);
                    }

                    _logger.LogInformation("KAFKA TRX CONSUMER: Committed {count} offset(s) successfully.", topicPartitionOffsets.Count);

                    break;
                }
                catch (KafkaException kex)
                {
                    _logger.LogWarning("KAFKA TRX CONSUMER: Attempt {AttemptNo}: Failed to commit offsets: {Message}", attemptNo + 1, kex.Message);

                    if (attemptNo < _consumerSettings.MaxConsumerRetries - 1)
                    {
                        int delayMs = GetExponentialDelay(attemptNo);
                        _logger.LogInformation(" KAFKA TRX CONSUMER:Retrying commit in {delay}ms...", delayMs);

                        await Task.Delay(delayMs, token);
                    }
                    else
                    {
                        _logger.LogInformation("KAFKA TRX CONSUMER: Failed to commit offsets after {counter} attempts.", _consumerSettings.MaxConsumerRetries);
                    }
                }
            }
        }

        private async Task CloseAndDisposeConsumerAndProducer()
        {
            try
            {
                await CommitOffsets(CancellationToken.None);
                _trxConsumer.Close();
                _trxConsumer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogInformation("KAFKA TRX CONSUMER: Error closing and shutting down consumer and dlqProducer");
            }
        }

        private int GetExponentialDelay(int attemptNo)
        {
            return (int)Math.Pow(2, attemptNo) * 100;
        }
    }
}
