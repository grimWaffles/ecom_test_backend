using Confluent.Kafka;
using System.ComponentModel.DataAnnotations;

namespace OrderServiceGrpc.Kafka
{
    public class KafkaConsumerSettings
    {
        //Main Consumer
        [Required]
        public string BootstrapServer { get; set; } = null!;
        [Required]
        public string GroupId { get; set; } = null!;

        public bool EnableAutoCommit { get; set; } = true;
        public bool EnableAutoOffsetStore { get; set; }
        public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Earliest;
        public string[] TopicsToConsume { get; set; } = [];
        public int AutoCommitIntervalInMs { get; set; } = 5000;
        public int MaxConsumerRetries { get; set; } = 3;
        public int ConsumerMessageBatchSize { get; set; } = 100;
        public int MaxDelayBetweenCommitsInMs { get; set; } = 50;

        //Dlq Settings
        public string[] DlqTopics { get; set; } = [];
        public Acks DlqAcks { get; set; } = Acks.All;
        public bool DlqIdempotence { get; set; }
        public int DlqMessageTimeoutMs { get; set; } = 5000;

        public string CatchAllDlqTopic { get; set; }
    }
}
