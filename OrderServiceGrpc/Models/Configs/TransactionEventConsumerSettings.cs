namespace OrderServiceGrpc.Models.Configs
{
    public class TransactionEventConsumerSettings
    {
        public string GroupId { get; set; }
        public string[] TopicsToConsume { get; set; }
        public string[] TopicsToProduce { get; set; }
    }
}
