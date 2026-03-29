
using Confluent.Kafka;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using OrderServiceGrpc.Helpers.cs;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.ConfigModels;
using OrderServiceGrpc.Models.Configs;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Repository;
using OrderServiceGrpc.Services;
using System.Collections.Concurrent;
using System.Text.Json;

namespace OrderServiceGrpc.Kafka
{
    public class OrderEventConsumer : BackgroundService
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

        // Kafka consumer for main topics
        private IConsumer<string, string> _consumer;

        //Global Kafka Settings
        private KafkaSettings _kafkaSettings;

        private OrderEventConsumerSettings _orderEventConsumerSettings;

        private KafkaEventProducer _kafkaProducer;

        private string _kafkaBootstrapServer;

        public OrderEventConsumer(ILogger<OrderEventConsumer> logger, IServiceProvider serviceProvider, IOptions<KafkaSettings> kafkaSettings, IOptions<KafkaConsumerSettings> kafkaConsumerSettings, IOptions<OrderEventConsumerSettings> ecSettings, KafkaEventProducer kafkaEventProducer)
        {
            _logger = logger;

            _serviceProvider = serviceProvider;

            _logger.LogInformation("KAFKA ORDER CONSUMER: Kafka order consumer constructor started...");

            // Load Kafka bootstrap server, topics, and DLQ topics from configuration
            _consumerSettings = kafkaConsumerSettings.Value;

            _logger.LogInformation("KAFKA ORDER CONSUMER: Kafka order consumer settings loaded...");

            _kafkaSettings = kafkaSettings.Value;
            _orderEventConsumerSettings = ecSettings.Value;

            _kafkaBootstrapServer = _kafkaSettings.Mode == "local" ? _kafkaSettings.BootstrapServerLocal : _kafkaSettings.BootstrapServerDocker;

            _kafkaProducer = kafkaEventProducer;

            _logger.LogInformation("KAFKA ORDER CONSUMER: Kafka order consumer constructor completed...");
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

                if (string.IsNullOrWhiteSpace(_consumerSettings.OrderGroupId))
                    throw new ArgumentException("Kafka GroupId is missing from configuration.");

                if (_orderEventConsumerSettings.TopicsToConsume.Length == 0)
                    throw new ArgumentException("No Kafka topics specified in configuration.");

                if (_orderEventConsumerSettings.DlqTopicsToProduce.Length == 0)
                    throw new ArgumentException("No Kafka DLQ topics specified in configuration.");

                // Consumer configuration
                _consumerConfig = new ConsumerConfig()
                {
                    BootstrapServers = _kafkaBootstrapServer,
                    GroupId = _consumerSettings.OrderGroupId,
                    AutoOffsetReset = _consumerSettings.AutoOffsetReset, // Start from beginning if no committed offsets
                    EnableAutoCommit = _consumerSettings.EnableAutoCommit,                   // We'll commit manually after successful processing
                    EnableAutoOffsetStore = _consumerSettings.EnableAutoOffsetStore,              // We'll explicitly store offsets after processing
                    AutoCommitIntervalMs = _consumerSettings.AutoCommitIntervalInMs
                };

                // Initialize main topic consumer and subscribe
                _consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();

                _consumer.Subscribe(_orderEventConsumerSettings.TopicsToConsume);

                _logger.LogInformation("KAFKA ORDER CONSUMER: Initialized order consumer service successfully");
            }

            catch (Exception e)
            {
                _logger.LogInformation($"KAFKA ORDER CONSUMER: Order consumer failed to subscribe to topics. Exception: {e.Message}", e.StackTrace);
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

                    ConsumeResult<string, string> result = _consumer.Consume(TimeSpan.FromMilliseconds(100));

                    //If result is null or empty
                    if (result == null)
                    {
                        await Task.Delay(100, stoppingToken); // prevent CPU spin
                        continue;
                    }

                    //Validate message topic
                    bool topicExists = Array.Exists(_orderEventConsumerSettings.TopicsToConsume, x => x == result.Topic);

                    //Implementing Max Retries if valid topic
                    if (topicExists)
                    {
                        processedMessage = await ProcessMessageResult(result, stoppingToken);
                    }

                    //Forwarding to the next step of the saga or DLQ Processing
                    if (processedMessage.Status && topicExists)
                    {
                        await ProcessNextEventInSaga(processedMessage.Order,result.Topic, stoppingToken);
                    }
                    else
                    {
                        await SendToDlq(result, topicExists, processedMessage, stoppingToken);
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

        private async Task<ConsumerResponseModel> ProcessMessageResult(ConsumeResult<string, string> result, CancellationToken stoppingToken)
        {
            ConsumerResponseModel repoResponse = new ConsumerResponseModel();

            for (int attemptNo = 0; attemptNo < _consumerSettings.MaxConsumerRetries; attemptNo++)
            {
                try
                {
                    repoResponse = await ProcessOrderEvent(result, stoppingToken);

                    if (repoResponse.Status == true)
                    {
                        return repoResponse;
                    }

                    _logger.LogInformation($"KAFKA ORDER CONSUMER: Processing failed for topic '{result.Topic}' by order consumer. Retrying...");

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
            return new ConsumerResponseModel()
            {
                Status = false,
                Message = $"KAFKA ORDER CONSUMER: Failed to process message for topic '{result.Topic}', Key: {result.Message.Key ?? "####"} after {_consumerSettings.MaxConsumerRetries} attempts. Error: {repoResponse.Message}",
                StackTrace = repoResponse.StackTrace
            };
        }

        private async Task<ConsumerResponseModel> ProcessOrderEvent(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            try
            {
                if (result.Topic.ToLower().Contains("order"))
                {
                    CreateOrderRequestDto request = JsonSerializer.Deserialize<CreateOrderRequestDto>(result.Message.Value) ?? new CreateOrderRequestDto();

                    if (request.Order != null && request.Order.Items.Count() > 0)
                    {
                        OrderModel orderModel = OrderMapper.DtoToEntity(request.Order);
                        int userId = request.UserId;

                        using (var scope = _serviceProvider.CreateScope())
                        {
                            IOrderProcessorService processorService = scope.ServiceProvider.GetRequiredService<IOrderProcessorService>();

                            ConsumerResponseModel repoResponse = (result.Topic) switch
                            {
                                "order-create" => await processorService.CreateOrder(request.Order, userId),
                                "order-update" => await processorService.UpdateOrder(request.Order, userId),
                                "order-delete" => await processorService.DeleteOrder(request.Order.Id, userId),
                                _ => new ConsumerResponseModel()
                                {
                                    Status = false,
                                    Message = $"KAFKA ORDER CONSUMER: Invalid topic provided in message={result.Topic}"
                                }
                            };

                            repoResponse.Order = OrderMapper.EntityToOrderDto(orderModel);

                            return repoResponse;
                        }
                    }

                    else
                    {
                        _logger.LogInformation($"KAFKA ORDER CONSUMER: Consumer has null order to process");
                    }
                }

                else if (result.Topic.ToLower().Contains("transaction"))
                {
                    //This is to ensure data consistency between the order and transaction tables.
                    //For example, if we receive a transaction-create event for a transaction that has failed, we can update the corresponding order to reflect the failed transaction status.
                    //This will be important for accurate reporting and analytics on order statuses and transaction outcomes.


                    //TODO: Implement CUD for order table operations if transactions have failed to be processed by the transaction consumer.
                }

                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = $"KAFKA ORDER CONSUMER: Invalid message provided topic:{result.Topic}",
                };
            }
            catch (Exception e)
            {
                return new ConsumerResponseModel()
                {
                    Status = false,
                    Message = $"KAFKA ORDER CONSUMER: Invalid topic provided in message:{result.Topic}. STACKTRACE: {e.StackTrace}",
                    StackTrace = $"StackTrace: {e.StackTrace}",
                };
            }
        }

        private async Task<bool> ProcessNextEventInSaga(OrderDto dto, string originalTopic, CancellationToken token)
        {
            try
            {
                //Process the order dto for transaction information
                OrderConsumerMessage message = new OrderConsumerMessage()
                {
                    UserId = dto.UserId,
                    Amount = dto.NetAmount,
                    OrderId = dto.Id
                };

                string payload = JsonSerializer.Serialize(message);

                string topic = originalTopic.ToLower() switch
                {
                    "order-create" => _orderEventConsumerSettings.TopicsToProduce.FirstOrDefault(x => x == "order-add-success") ?? "",
                    "order-update" => _orderEventConsumerSettings.TopicsToProduce.FirstOrDefault(x => x == "order-update-success") ?? "",
                    "order-delete" => _orderEventConsumerSettings.TopicsToProduce.FirstOrDefault(x => x == "order-delete-success") ?? "",
                    _ => ""
                };

                if (topic == null || topic == "")
                {
                    _logger.LogInformation($"KAFKA ORDER CONSUMER: Processing next event in saga has been cancelled because topic cannot be found");
                    return false;
                }

                //Produce the message
                await _kafkaProducer.ProduceEventAsync(topic, dto.Id.ToString(), payload, token);

                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _logger.LogInformation($"KAFKA ORDER CONSUMER: Processing next event in saga has been cancelled by user for topic:{originalTopic}, key:{dto.Id}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"KAFKA ORDER CONSUMER EXCEPTION: Failed to process next event in saga for topic:{originalTopic}, key:{dto.Id}. ErrorMessage: {ex.Message}");
                return false;
            }
        }


        private async Task SendToDlq(ConsumeResult<string, string> result, bool topicExists, ConsumerResponseModel repoResponse, CancellationToken stoppingToken)
        {
            string dlqTopic = ""; bool produceDlq = false;

            //Check if DLQ topic exists for that topic, if we get an empty topic we know what to do
            if (topicExists)
            {
                dlqTopic = _orderEventConsumerSettings.DlqTopicsToProduce.Where(x => x == result.Topic + "-dlq").FirstOrDefault() ?? "";
                dlqTopic = dlqTopic == "" ? $"No DLQ topic found for topic:{result.Topic}" : dlqTopic;

                produceDlq = dlqTopic == "" ? false : true;
            }
            else
            {
                dlqTopic = $"Invalid Topic Found:{result.Topic}";
                produceDlq = false;
            }

            // If processing fails, prepare a DLQ message with metadata
            DeadLetterQueueMessage dlqMessage = new DeadLetterQueueMessage
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
                    await _kafkaProducer.ProduceDlqEventAsync(result.Topic, dlqMessage, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation($"KAFKA ORDER CONSUMER EXCEPTION: Failed to produce DLQ message for {result.Topic}. ErrorMessage: {ex.Message}");
                }
            }
            else
            {
                _logger.LogInformation($"KAFKA ORDER CONSUMER: Failed to process DLQ Message for order consumer: {result.Topic}. Sending to Catch-All...");

                // Produce failed message to CATCH-ALL-DLQ
                try
                {
                    await _kafkaProducer.ProduceDlqEventAsync(_consumerSettings.CatchAllDlqTopic, dlqMessage, stoppingToken);
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
                _logger.LogInformation($"KAFKA ORDER CONSUMER: Attemp#{attemptNo + 1} to commit order consumer result...");

                try
                {
                    _consumer.Commit(topicPartitionOffsets);

                    // remove committed offsets from tracking dictionary
                    foreach (var tpo in topicPartitionOffsets)
                    {
                        _processedOffsets.TryRemove(tpo.TopicPartition, out _);
                    }

                    _logger.LogInformation($"KAFKA ORDER CONSUMER: Committed {topicPartitionOffsets.Count} offset(s) successfully by order consumer.");

                    break;
                }
                catch (KafkaException kex)
                {
                    _logger.LogWarning($"KAFKA ORDER CONSUMER: Attempt {attemptNo + 1}: Failed to commit offsets for order consumer: {kex.Message}");

                    if (attemptNo < _consumerSettings.MaxConsumerRetries - 1)
                    {
                        int delayMs = GetExponentialDelay(attemptNo);
                        _logger.LogInformation($"KAFKA ORDER CONSUMER: Retrying commit by order consumer in {delayMs}ms...");

                        await Task.Delay(delayMs, token);
                    }
                    else
                    {
                        _logger.LogInformation($"KAFKA ORDER CONSUMER: Failed to commit offsets after {_consumerSettings.MaxConsumerRetries} attempts by order consumer.");
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
            }
            catch (Exception ex)
            {
                _logger.LogInformation("KAFKA ORDER CONSUMER: Error closing and shutting down consumer and dlqProducer");
            }
        }

        private int GetExponentialDelay(int attemptNo)
        {
            return (int)Math.Pow(2, attemptNo) * 100;
        }

    }
}
