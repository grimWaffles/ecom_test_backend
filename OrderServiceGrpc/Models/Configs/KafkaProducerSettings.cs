using Confluent.Kafka;

namespace OrderServiceGrpc.Models.ConfigModels
{
    public class KafkaProducerSettings
    {
        public int MessageTimeoutMs { get; set; }
        public int RetryAfterDelayMs { get; set; }
        public int MaxNoOfRetries { get; set; }
        public int TotalPartitions { get; set; }
        public bool EnableIdempotence { get; set; }
        public Acks Acks { get; set; } = Acks.All;
    }
}
