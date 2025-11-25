namespace API_Gateway.Models
{
    public class KafkaProducerResult
    {
        public bool Status { get; set; }
        public string Topic { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public int? PartitionNumber { get; set; }
    }
}
