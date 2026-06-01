using Azure.Core;
using OrderServiceGrpc.Helpers.Converters;
using OrderServiceGrpc.Kafka.Producers;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Services.BackgroundServices
{
    public class OutboxExecutor : BackgroundService
    {
        private readonly ILogger<OutboxExecutor> _logger;
        private readonly IKafkaEventProducer _kafkaEventProducer;
        private readonly IServiceProvider _serviceProvider;
        private const int DelayInSeconds = 6000;

        public OutboxExecutor(ILogger<OutboxExecutor> logger, IKafkaEventProducer kafkaEventProducer, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _kafkaEventProducer = kafkaEventProducer;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(DelayInSeconds, stoppingToken);
                    await RunOutboxExecutorAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("OutboxExecutor is stopping due to cancellation request.");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred in OutboxExecutor.");
                    throw;
                }
            }
        }

        private async Task RunOutboxExecutorAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Running outbox executor at {Time}", DateTime.UtcNow);

                IEnumerable<OrderOutbox> outboxRecords = new List<OrderOutbox>();

                //get the outbox records that are scheduled to be published after the last execution time
                using (IServiceScope scope = _serviceProvider.CreateAsyncScope())
                {
                    IOrderOutboxService outboxService = scope.ServiceProvider.GetRequiredService<IOrderOutboxService>();

                    outboxRecords = await outboxService.GetRecentRecordsToPublishAfterDateAsync(DateTime.UtcNow.AddSeconds(DelayInSeconds * -1 * 2));

                    if (!outboxRecords.Any())
                    {
                        _logger.LogInformation("No records to publish at this time");
                        return;
                    }

                    //Produce the kafka message
                    foreach (OrderOutbox orderOutbox in outboxRecords)
                    {
                        bool eventProduced = await _kafkaEventProducer.ProduceEventAsync(orderOutbox.Topic, orderOutbox.PartitionKey, orderOutbox.Payload, stoppingToken);

                        if (eventProduced)
                        {
                            orderOutbox.ProcessedAt = DateTime.UtcNow;
                            await outboxService.UpdateAsync(orderOutbox);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("OutboxExecutor is stopping due to cancellation request.");
            }
            catch (Exception e)
            {
                _logger.LogError("An error occurred while running the OutboxExecutor. {Error}. {Stacktrace}", e.Message, e.StackTrace);
            }
        }
    }
}
