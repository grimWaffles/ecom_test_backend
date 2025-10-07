
using Confluent.Kafka;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Repository;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using static Confluent.Kafka.ConfigPropertyNames;

namespace OrderServiceGrpc.Services
{
    public class OrderEventConsumer : BackgroundService
    {
        // Kafka connection details
        private readonly string _bootstrapServer;

        // List of topics this consumer subscribes to
        private readonly string[] _topic;

        // Dead Letter Queue topics (for failed messages)
        private readonly string[] _dqlTopic;

        // Consumer group ID, used for Kafka offset tracking
        private readonly string _groupId;

        // Repository for processing events (e.g., inserting into DB)
        private readonly IOrderRepository _orderRepository;

        // Kafka consumer configuration
        private readonly ConsumerConfig _consumerConfig;

        // Kafka producer configuration for DLQ
        private readonly ProducerConfig _producerConfig;

        // Producer used to send messages to DLQ topics
        private readonly IProducer<string, string> _dlqProducer;

        // Kafka consumer for main topics
        private readonly IConsumer<string, string> _consumer;

        // Tracks offsets of successfully processed messages for manual commit
        private readonly ConcurrentDictionary<TopicPartition, Offset> _processedOffsets = new();

        public OrderEventConsumer(IConfiguration configuration, IOrderRepository orderRepository)
        {
            // Load Kafka bootstrap server, topics, and DLQ topics from configuration
            _bootstrapServer = configuration["Kafka:BootstrapServer"] ?? "";
            _topic = configuration.GetSection("Kafka:Topic").Get<string[]>() ?? Array.Empty<string>();
            _dqlTopic = configuration.GetSection("Kafka:DlqTopic").Get<string[]>() ?? Array.Empty<string>();

            _groupId = configuration["Kafka:GroupId"] ?? "";
            _orderRepository = orderRepository;

            // Consumer configuration
            _consumerConfig = new ConsumerConfig()
            {
                BootstrapServers = _bootstrapServer,
                GroupId = _groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest, // Start from beginning if no committed offsets
                EnableAutoCommit = false,                   // We'll commit manually after successful processing
                EnableAutoOffsetStore = false,              // We'll explicitly store offsets after processing
            };

            // Producer configuration for DLQ messages
            _producerConfig = new ProducerConfig()
            {
                BootstrapServers = _bootstrapServer,
                Acks = Acks.All,            // Wait for all replicas to acknowledge
                EnableIdempotence = true    // Ensure no duplicate DLQ messages
            };

            // Initialize DLQ producer
            _dlqProducer = new ProducerBuilder<string, string>(_producerConfig).Build();

            // Initialize main topic consumer and subscribe
            _consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();
            _consumer.Subscribe(_topic);

            Console.WriteLine("Initialized consumer service successfully");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Main consumer loop
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Consume messages (blocking call)
                    try
                    {
                        ConsumeResult<string, string> result = _consumer.Consume(TimeSpan.FromMilliseconds(100));

                        if (result == null)
                        {
                            await Task.Delay(200, stoppingToken); // prevent CPU spin
                            continue;
                        }
                        try
                        {
                            // Handle different topics dynamically
                            switch (result.Topic)
                            {
                                case "order-create":
                                    await OrderCreateEvent(result);
                                    break;

                                case "order-update":
                                    await OrderCreateEvent(result);
                                    break;

                                default:
                                    // Ignore unknown topics
                                    continue;
                            }

                            // After successful processing, store and track offset for manual commit
                            _consumer.StoreOffset(result);
                            _processedOffsets[result.TopicPartition] = result.Offset + 1;
                        }
                        catch (Exception e)
                        {
                            // If processing fails, prepare a DLQ message with metadata
                            var dlqMessage = new
                            {
                                OriginalTopic = result.Topic,
                                OriginalMessage = result.Message.Value,
                                Key = result.Message.Key,
                                Exception = e.Message,
                                StackTrace = e.StackTrace,
                                TimeStamp = DateTime.Now
                            };

                            // Construct DLQ topic name (one per original topic)
                            string dlqTopic = $"{result.Topic}-dlq";

                            // Produce failed message to DLQ
                            await _dlqProducer.ProduceAsync(dlqTopic, new Message<string, string>
                            {
                                Key = result.Message.Key.ToString(),
                                Value = JsonSerializer.Serialize(dlqMessage)
                            });

                            // Still store offset to prevent the poison message from blocking consumption
                            _consumer.StoreOffset(result);
                            _processedOffsets[result.TopicPartition] = result.Offset + 1;
                        }

                        finally
                        {
                            // Commit all tracked offsets to Kafka
                            List<TopicPartitionOffset> offsets = _processedOffsets
                                .Select(kv => new TopicPartitionOffset(kv.Key, kv.Value))
                                .ToList();

                            if (offsets.Any())
                            {
                                _consumer.Commit(offsets);
                                Console.WriteLine("Successfully consumed eventId: " + result.Message.Key);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Failed to consume result");
                        Console.WriteLine("ERROR: " + e.Message);
                        Console.WriteLine("STACKTRACE: " + e.StackTrace);
                        continue;
                    }
                }
            }
            catch (Exception e)
            {
                // Catch any unhandled exceptions in the consumer loop
                Console.WriteLine("Failed to Process message:");
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                // Graceful shutdown
                _consumer.Close();
                _consumer.Dispose();
                _dlqProducer.Flush();
                _dlqProducer.Dispose();
            }
        }

        private async Task OrderCreateEvent(ConsumeResult<string, string> eventValue)
        {
            try
            {
                // Deserialize message into domain event
                var model = JsonSerializer.Deserialize<OrderCreatedEvent>(eventValue.Message.Value);

                // Validate deserialization
                if (eventValue != null && model != null)
                {
                    // Insert event into repository (DB, etc.)
                    await _orderRepository.InsertOrderCreateEvent(Convert.ToInt32(eventValue.Message.Key));
                }
            }
            catch (Exception ex)
            {
                // Log processing exceptions
                Console.WriteLine(ex.StackTrace);
            }
        }
    }

}
