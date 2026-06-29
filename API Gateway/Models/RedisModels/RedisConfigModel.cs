namespace API_Gateway.Models.RedisModels
{
    public class RedisConfigModel
    {
        public const string SectionName = "Redis";
        public string LocalUrl { get; set; }
        public string DockerUrl { get; set; }
        public string Mode { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public string GetRedisConnectionString() => this.Mode == "docker" ? this.DockerUrl : this.LocalUrl;
    }
}
