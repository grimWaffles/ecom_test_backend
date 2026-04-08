namespace OrderServiceGrpc.Models.Configs
{
    public class OrderEventConsumerSettings
    {
        public string GroupId { get; set; }
        public string[] TopicsToConsume { get; set; }
        public string[] TopicsToProduce { get; set; }
        public string[] DlqTopicsToProduce { get; set; }
    }
}


