namespace OrderServiceGrpc.Models
{
    public class DatabaseConfig
    {
        public string Database { get; set; } = "" ;
        public string Mode { get; set; } = "" ;
    }

    public class DatabaseConnection
    {
        public string MySqlConnection { get; set; } = "" ;
        public string MySqlDockerConnection { get; set; } = "" ;
        public string SqlServerConnection { get; set; } = "" ;
        public string SqlServerDockerConnection { get; set; } = "" ;
    }
}