using API_Gateway.AuthHandlers;
using API_Gateway.Grpc;
using API_Gateway.Handlers;
using API_Gateway.Middlewares;
using API_Gateway.Models;
using API_Gateway.Repository;
using API_Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using System.Runtime.CompilerServices;

namespace API_Gateway.Helpers
{
    public static class DependencyResolver
    {
        public static void RegisterServices(this IServiceCollection services, IConfiguration config)
        {
            // ── External ─────────────────────────────────────────────────────────────
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IProductCategoryGrpcClient, ProductCategoryGrpcClient>();
            services.AddScoped<IProductGrpcClient, ProductGrpcClient>();
            services.AddScoped<ISellerGrpcClient, SellerGrpcClient>();
            services.AddScoped<IOrderGrpcClient, OrderGrpcClient>();
            services.AddScoped<ICustomerTransactionGrpcClient, CustomerTransactionGrpcClient>();

            // ── Repository ─────────────────────────────────────────────────────────────
            services.AddScoped<IRequestLogRepository, RequestLogRepository>();
            services.AddScoped<IAuthorizationHandler, RoleAuthorizationHandler>();
            services.AddScoped<IAuthorizationHandler, ReportAuthorizationHandler>();

            //Main Auth Policy Provider
            services.AddSingleton<IAuthorizationPolicyProvider, RolePermissionPolicyProvider>();

            // ── Service ────────────────────────────────────────────────────────────────
            services.AddScoped<IRequestLogService, RequestLogService>();
            services.AddSingleton<IRedisService, RedisService>();
        }

        public static void RegisterMiddleware(this IServiceCollection services)
        {
            services.AddScoped<TokenAuthorizationMiddleware>();
        }

        public static void RegisterConfigOptions(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<MicroServiceUrl>(config.GetSection("MicroServiceUrls"));
        }
    }
}
