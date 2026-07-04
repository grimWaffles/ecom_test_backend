namespace API_Gateway.Models
{
    public class JwtInternalSchemaOptions
    {
        public const string SectionName = "JwtInternalSchema";
        public string SigningKey { get; set; } = "";
        public string ValidIssuer { get; set; } = "";
        public string ValidAudience { get; set; } = "";
        public int ExpirationInSeconds { get; set; } = 20;
    }
}
