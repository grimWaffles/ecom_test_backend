namespace OrderServiceGrpc.Models.ConfigModels
{
    public class KafkaSettings
    {
        public string BootstrapServerLocal { get; set; }
        public string BootstrapServerDocker { get; set; }
        public string Mode { get; set; }
    }
}
