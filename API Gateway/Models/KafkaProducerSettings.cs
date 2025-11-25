namespace API_Gateway.Models
{
    public class KafkaProducerSettings
    {
        public string BootstrapServer { get; set; }
        public int MessageTimeoutMs { get; set; }
        public int RetryAfterDelayMs { get; set; }
        public int MaxNoOfRetries { get; set; }
        public int TotalPartitions { get; set; }
        public bool EnableIdempotence { get; set; }
        public int Acks { get; set; }
    }
}
