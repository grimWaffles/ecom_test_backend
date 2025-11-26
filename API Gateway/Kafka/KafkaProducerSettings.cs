using Confluent.Kafka;

namespace API_Gateway.Helpers
{
    public class KafkaProducerSettings
    {
        public string BootstrapServer { get; set; } = "";
        public int MessageTimeoutMs { get; set; }
        public int RetryAfterDelayMs { get; set; }
        public int MaxNoOfRetries { get; set; }
        public int TotalPartitions { get; set; }
        public bool EnableIdempotence { get; set; }
        public Acks Acks { get; set; } = Acks.All;
    }
}
