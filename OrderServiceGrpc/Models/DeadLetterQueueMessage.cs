namespace OrderServiceGrpc.Models
{
    public class DeadLetterQueueMessage
    {
        public string OriginalTopic { get; init; } = string.Empty;
        public string OriginalMessage { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;
        public string Exception { get; init; } = string.Empty;
        public string? StackTrace { get; init; }
        public DateTime TimeStamp { get; init; } = DateTime.UtcNow;
        public int Partition { get; init; }
        public long Offset { get; init; }
    }
}
