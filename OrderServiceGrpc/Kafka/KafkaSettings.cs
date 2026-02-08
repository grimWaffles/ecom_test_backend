namespace OrderServiceGrpc.Kafka
{
    public class KafkaSettings
    {
        public string BootstrapServerLocal { get; set; }
        public string BootstrapServerDocker { get; set; }
        public string[] Topics { get; set; }
        public string[] DlqTopic { get; set; }
        public string GroupId { get; set; }
        public string Mode { get; set; }
    }
}
