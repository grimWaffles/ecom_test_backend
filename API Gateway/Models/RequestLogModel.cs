namespace API_Gateway.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Security.Claims;

    public class RequestLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public DateTime RequestTimeUtc { get; set; }

        public string TraceIdentifier { get; set; } = null!;

        public long? UserId { get; set; }

        public string? Username { get; set; }

        public string? IpAddress { get; set; }

        public string Method { get; set; } = null!;

        public string Path { get; set; } = null!;

        public string? QueryString { get; set; }

        public string? UserAgent { get; set; }

        public int StatusCode { get; set; }

        public long DurationMs { get; set; }

        public bool IsAuthenticated { get; set; }

        public string? ErrorMessage { get; set; }

        public static RequestLog FromHttpContext(
            HttpContext context,
            long durationMs,
            Exception? exception = null)
        {
            var userIdClaim =
                context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value
                ?? context.User.FindFirst("userId")?.Value;

            long.TryParse(userIdClaim, out var userId);

            return new RequestLog
            {
                RequestTimeUtc = DateTime.UtcNow,

                TraceIdentifier = context.TraceIdentifier,

                UserId = userId > 0 ? userId : null,

                Username = context.User.Identity?.Name,

                IpAddress = GetClientIp(context),

                Method = context.Request.Method,

                Path = context.Request.Path.Value ?? string.Empty,

                QueryString = context.Request.QueryString.Value,

                UserAgent = context.Request.Headers.UserAgent.ToString(),

                StatusCode = context.Response.StatusCode,

                DurationMs = durationMs,

                IsAuthenticated =
                    context.User.Identity?.IsAuthenticated ?? false,

                ErrorMessage = exception?.Message
            };
        }

        private static string? GetClientIp(HttpContext context)
        {
            var forwardedFor = context.Request.Headers["X-Forwarded-For"]
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }
}
