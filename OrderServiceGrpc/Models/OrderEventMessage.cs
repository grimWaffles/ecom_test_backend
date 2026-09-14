using System.Text.Json.Serialization;

namespace OrderServiceGrpc.Models
{
    public class OrderEventMessage
    {
        [JsonPropertyName("Id")]
        public int OrderId { get; set; }

        [JsonPropertyName("NetAmount")]
        public double Amount { get; set; }

        [JsonPropertyName("UserId")]
        public int UserId { get; set; }
    }
}
