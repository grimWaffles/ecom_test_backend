using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace API_Gateway.Middlewares
{
    public class TokenAuthorizationMiddleware : IMiddleware
    {
        private readonly ILogger<TokenAuthorizationMiddleware> _logger;
        public TokenAuthorizationMiddleware(ILogger<TokenAuthorizationMiddleware> logger)
        {
            _logger = logger;
        }

        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            _logger.LogInformation("TokenAuthorization middleware invoked for path: {Path}", context.Request.Path);
            
            return next(context);
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class TokenAuthorizationExtensions
    {
        public static IApplicationBuilder UseTokenAuthorizationMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenAuthorizationMiddleware>();
        }
    }
}
