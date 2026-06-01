namespace OrderServiceGrpc.Models.ConfigModels
{
    public class DatabaseConfig
    {
        public string Database { get; set; } = "" ;
        public string Mode { get; set; } = "" ;
    }

    public class DatabaseConnection
    {
        public string SqlServerHomeConnection { get; set; } = "" ;
        public string SqlServerHomeDockerConnection { get; set; } = "" ;
        public string SqlServerWorkConnection { get; set; } = "" ;
        public string SqlServerWorkDockerConnection { get; set; } = "" ;
    }
}