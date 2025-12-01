
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Repository;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Text.Json;
using Z.BulkOperations;
using static Confluent.Kafka.ConfigPropertyNames;

namespace OrderServiceGrpc.Services
{
    public class KafkaOrderConsumer : BackgroundService
    {
        //IServiceProvider for the order repo service (A scoped service DI'd into a singleton)
        private readonly IServiceProvider _serviceProvider;

        // Local commit tracking (topic-partition -> offset to commit)
        private readonly ConcurrentDictionary<TopicPartition, Offset> _processedOffsets = new();

        //Kafka Consumer Settings
        private readonly KafkaConsumerSettings _consumerSettings;

        // Kafka consumer configuration
        private readonly ConsumerConfig _consumerConfig;

        // Kafka producer configuration for DLQ
        private readonly ProducerConfig _dlqProducerConfig;

        // Producer used to send messages to DLQ topics
        private IProducer<string, string> _dlqProducer;

        // Kafka consumer for main topics
        private IConsumer<string, string> _consumer;

        public KafkaOrderConsumer(IServiceProvider serviceProvider, IOptions<KafkaConsumerSettings> kafkaConsumerSettings)
        {
            _serviceProvider = serviceProvider;

            // Load Kafka bootstrap server, topics, and DLQ topics from configuration
            _consumerSettings = kafkaConsumerSettings.Value;

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

            Console.WriteLine("Initialized consumer service successfully");
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            // Initialize DLQ producer
            _dlqProducer = new ProducerBuilder<string, string>(_dlqProducerConfig).Build();

            // Initialize main topic consumer and subscribe
            _consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();

            _consumer.Subscribe(_consumerSettings.TopicsToConsume);

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //Commit Interval
            DateTime lastCommitTime = DateTime.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<string, string> result = _consumer.Consume(stoppingToken);

                    //If result is null or empty
                    if (result == null)
                    {
                        await Task.Delay(200, stoppingToken); // prevent CPU spin
                        continue;
                    }

                    bool processedMessage = false;
                    RepoResponseModel repoResponse = new RepoResponseModel();

                    //Validate message topic
                    string topic = _consumerSettings.TopicsToConsume.Where(x => x == result.Topic).FirstOrDefault() ?? "";
                    
                    //Implementing Max Retries if valid topic
                    if (topic != "")
                    {
                        for (int i = 0; i < _consumerSettings.MaxConsumerRetries; i++)
                        {
                            try
                            {
                                repoResponse = await ProcessOrderEvent(result, stoppingToken);

                                if (repoResponse.Status == true)
                                {
                                    processedMessage = true;
                                    break;
                                }

                                await Task.Delay(1500, stoppingToken);
                            }
                            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                if (i < _consumerSettings.MaxConsumerRetries)
                                {
                                    await Task.Delay(1500, stoppingToken);
                                }
                            }
                        }
                    }

                    if (!processedMessage || topic == "")
                    {
                        //Check if DLQ topic exists for that topic 
                        string dlqTopic = _consumerSettings.TopicsToConsume.Where(x => x == result.Topic).FirstOrDefault() + "-dlq" ?? "";
                        dlqTopic = dlqTopic == "" ? $"Invalid Topic Found:{result.Topic}" : dlqTopic;

                        // If processing fails, prepare a DLQ message with metadata
                        var dlqMessage = new
                        {
                            OriginalTopic = result.Topic,
                            OriginalMessage = result.Message.Value,
                            Key = result.Message.Key,
                            Exception = repoResponse.Message,
                            StackTrace = repoResponse.StackTrace,
                            TimeStamp = DateTime.Now
                        };

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
                            Console.WriteLine($"Failed to produce DLQ message for {result.Topic}");
                        }
                    }

                    _consumer.StoreOffset(result);
                    _processedOffsets[result.TopicPartition] = result.Offset + 1;

                    //check whether it is time to commit the offsets
                    if (DateTime.UtcNow- lastCommitTime>= _consumerSettings.MaxDelayBetweenCommitsInMs || _processedOffsets.Count()>= _consumerSettings.ConsumerMessageBatchSize)
                    {
                        CommitOffsets();
                    }

                }
                catch (ConsumeException cex)
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    CommitOffsets();
                    throw new Exception($"Fatal Error: {ex.Message}");
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _consumer.Close();
            _consumer.Dispose();
            _dlqProducer.Flush();
            _dlqProducer.Dispose();

            return base.StopAsync(cancellationToken);
        }

        private void CommitOffsets()
        {
            var topicPartitionOffsets = _processedOffsets
            .Select(kv => new TopicPartitionOffset(kv.Key, kv.Value))
            .ToList();


            if (!topicPartitionOffsets.Any()) return;


            try
            {
                _consumer.Commit(topicPartitionOffsets);
                

                // remove committed offsets from tracking dictionary
                foreach (var tpo in topicPartitionOffsets)
                {
                    _processedOffsets.TryRemove(tpo.TopicPartition, out _);
                }
            }
            catch (KafkaException kex)
            {
               
            }
        }

        private async Task<RepoResponseModel> ProcessOrderEvent(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            RepoResponseModel repoResponse = new RepoResponseModel();

            using (var scope = _serviceProvider.CreateScope())
            {
                IOrderRepository orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

                repoResponse = (result.Topic) switch
                {
                    "order-create" => await OrderCreateEvent(result, orderRepository),
                    "order-update" => await OrderUpdateEvent(result, orderRepository),
                    _=> new RepoResponseModel()
                    {
                        Status = false,
                        Message = $"Error: Invalid topic provided in message={result.Topic}"
                    }
                };
            }

            return repoResponse;
        }

        private async Task<RepoResponseModel> OrderCreateEvent(ConsumeResult<string, string> eventValue, IOrderRepository orderRepository)
        {
            try
            {
                // Deserialize message into domain event
                var model = JsonSerializer.Deserialize<OrderCreatedEvent>(eventValue.Message.Value);

                return await orderRepository.InsertOrderCreateEvent(Convert.ToInt32(eventValue.Message.Key));
            }
            catch (Exception ex)
            {
                return new RepoResponseModel()
                {
                    Status = false,
                    Message = ex.Message
                };
            }
        }

        private async Task<RepoResponseModel> OrderUpdateEvent(ConsumeResult<string, string> eventValue, IOrderRepository orderRepository)
        {
            try
            {
                // Deserialize message into domain event
                var model = JsonSerializer.Deserialize<OrderCreatedEvent>(eventValue.Message.Value);

                return await orderRepository.InsertOrderCreateEvent(Convert.ToInt32(eventValue.Message.Key));
            }
            catch (Exception ex)
            {
                return new RepoResponseModel()
                {
                    Status = false,
                    Message = ex.Message
                };
            }
        }
    }

}
