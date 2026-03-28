namespace API_Gateway.Kafka
{
    public class KafkaGlobalSetting
    {
        public string BootstrapServerLocal { get; set; } = "";
        public string BootstrapServerDocker { get; set; } = "";
        public string Mode { get; set; } = "";
    }
}
