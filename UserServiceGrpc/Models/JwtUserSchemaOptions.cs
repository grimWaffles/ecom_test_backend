namespace UserServiceGrpc.Models
{
    public class JwtUserSchemaOptions
    {
        public const string SectionName = "JwtUserSchema";
        public string SigningKey { get; set; } = "";
        public string ValidIssuer { get; set; } = "";
        public string ValidAudience { get; set; } = "";
        public int ExpirationInSeconds { get; set; } = 20;
    }
}
