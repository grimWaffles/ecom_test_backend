namespace OrderServiceGrpc.Models.ConfigModels
{
    public class KafkaSettings
    {
        public string BootstrapServerLocal { get; set; }
        public string BootstrapServerDocker { get; set; }
        public string[] OrderTopic { get; set; }
        public string[] OrderDlqTopic { get; set; }
        public string[] TrxTopic { get; set; }
        public string[] TrxDlqTopic { get; set; }
        public string GroupId { get; set; }
        public string Mode { get; set; }
    }
}
