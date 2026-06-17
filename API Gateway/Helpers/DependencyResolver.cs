using API_Gateway.AuthHandlers.Handlers;
using API_Gateway.AuthHandlers.PolicyProviders;
using API_Gateway.Grpc;
using API_Gateway.Middlewares;
using API_Gateway.Models;
using API_Gateway.Repository;
using API_Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using Grpc.Net.ClientFactory;
using ApiGateway.Protos;
using API_Gateway.AuthHandlers.Interceptors;

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

            // ── Main Auth Policy Provider
            services.AddSingleton<IAuthorizationPolicyProvider, RolePermissionPolicyProvider>();
            services.AddSingleton<JwtForwardingInterceptor>();

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

        public static void RegisterGrpcServices(this IServiceCollection services, IConfiguration config)
        {
            //load the microservice urls
            MicroServiceUrl serviceUrls = config.GetSection("MicroServiceUrls").Get<MicroServiceUrl>();

            //Register the services
            services.AddGrpcClient<User.UserClient>(options =>
            {
                options.Address = new Uri(serviceUrls.GetUserServiceUrl());
            });

            services.AddGrpcClient<Seller.SellerClient>(options =>
            {
                options.Address = new Uri(serviceUrls.GetProductServiceUrl());
            });

            services.AddGrpcClient<ProductService.ProductServiceClient>(options =>
            {
                options.Address = new Uri(serviceUrls.GetProductServiceUrl());
            });

            services.AddGrpcClient<ProductCategory.ProductCategoryClient>(options =>
            {
                options.Address = new Uri(serviceUrls.GetProductServiceUrl());
            });

            services.AddGrpcClient<OrderGrpcService.OrderGrpcServiceClient>(options =>
            {
                options.Address = new Uri(serviceUrls.GetOrderServiceUrl());
            });
        }
    }
}
