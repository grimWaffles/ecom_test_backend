namespace OrderServiceGrpc.Kafka
{
    public class KafkaSettings
    {
        public string BootstrapServerLocal { get; set; }
        public string BootstrapServerDocker { get; set; }
        public string[] OrderTopic { get; set; }
        public string[] OrderDlqTopic { get; set; }
        public string GroupId { get; set; }
        public string Mode { get; set; }
    }
}
