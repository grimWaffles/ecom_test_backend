using API_Gateway.AuthHandlers.Handlers;
using API_Gateway.AuthHandlers.PolicyProviders;
using API_Gateway.CacheService;
using API_Gateway.Database;
using API_Gateway.Grpc;
using API_Gateway.Interceptors;
using API_Gateway.Middlewares;
using API_Gateway.Models;
using API_Gateway.Repository;
using API_Gateway.Services;
using ApiGateway.Protos;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace API_Gateway.Helpers
{
    public static class DependencyResolver
    {
        public static void ConfigureDatabases(this IServiceCollection services, IConfiguration configuration)
        {
            //Configure the database context
            string dbType = configuration["DatabaseConfig:Database"] ?? "";
            string mode = configuration["DatabaseConfig:Mode"] ?? "";
            string dbKey = "";
            string connectionString = "";

            if (dbType == "" || mode == "")
            {
                throw new InvalidOperationException("Database configuration not set up correctly.");
            }

            dbKey = (dbType.ToLower(), mode.ToLower()) switch
            {
                ("work", "local") => "SqlServerWorkConnection",
                ("work", "docker") => "SqlServerWorkDockerConnection",
                ("home", "local") => "SqlServerHomeConnection",
                ("home", "docker") => "SqlServerHomeDockerConnection",
                _ => ""
            };

            if (dbKey == "")
            {
                throw new InvalidOperationException("Database key not found.");
            }

            connectionString = configuration.GetConnectionString(dbKey) ?? "";

            if (connectionString == "")
            {
                throw new InvalidOperationException("Database connection string not found.");
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString)
            );
        }

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
            services.AddSingleton<ICustomCacheService, CustomCacheService>();
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
            })
                ////Option 1 : Use the call credentials to attach the token to the requests made from this client
                //// USES HTTPS ONLY
                //.AddCallCredentials(async (context, metadata, serviceProvider) =>
                //{
                //    IHttpContextAccessor httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

                //    if (httpContextAccessor.HttpContext != null)
                //    {
                //        string? token = await httpContextAccessor.HttpContext.GetTokenAsync("access_token");

                //        if (!string.IsNullOrEmpty(token))
                //        {
                //            metadata.Add("Authorization", $"Bearer {token}");
                //        }
                //    }
                //});
                
                //Option 2: The recommended/ cleaner approach is to use a seperate interceptor class.
                //Adds more flexibility and the options to add logging and what not.
                .AddInterceptor<JwtForwardingInterceptor>(); //UserService uses the main token forwarding.

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
