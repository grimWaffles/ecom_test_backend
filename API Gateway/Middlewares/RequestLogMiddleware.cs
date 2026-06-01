using API_Gateway.Models;
using API_Gateway.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace API_Gateway.Middlewares
{
    public class RequestLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLogMiddleware> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public RequestLogMiddleware(
            RequestDelegate next,
            ILogger<RequestLogMiddleware> logger,
            IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation(
                    "Request started: {Method} {Path}",
                    httpContext.Request.Method,
                    httpContext.Request.Path);

                await _next(httpContext);

                stopwatch.Stop();

                _logger.LogInformation(
                    "Request completed: {Method} {Path} - Status: {StatusCode} - Duration: {ElapsedMilliseconds}ms",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    httpContext.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(
                    ex,
                    "Request failed: {Method} {Path} - Duration: {ElapsedMilliseconds}ms",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    stopwatch.ElapsedMilliseconds);

                throw;
            }
            finally
            {
                await LogRequestAsync(httpContext, stopwatch.ElapsedMilliseconds);
            }
        }

        private async Task LogRequestAsync(HttpContext httpContext, long elapsedMilliseconds)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();

                IRequestLogService requestLogService = scope
                    .ServiceProvider
                    .GetRequiredService<IRequestLogService>();

                RequestLog requestLog = RequestLog.FromHttpContext(
                    httpContext,
                    (int)elapsedMilliseconds,
                    null);

                await requestLogService.CreateAsync(
                    requestLog,
                    CancellationToken.None);

                _logger.LogDebug(
                    "Request log created: {@RequestLog}",
                    requestLog);
            }
            catch (Exception ex)
            {
                // Never let logging failures bubble up and break the pipeline
                _logger.LogError(
                    ex,
                    "Failed to persist request log for {Method} {Path}",
                    httpContext.Request.Method,
                    httpContext.Request.Path);
            }
        }
    }

    public static class RequestLogMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogMiddleware(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLogMiddleware>();
        }
    }
}