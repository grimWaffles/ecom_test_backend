using API_Gateway.AuthHandlers.Handlers;
using API_Gateway.AuthHandlers.PolicyProviders;
using API_Gateway.Database;
using API_Gateway.Filters;
using API_Gateway.Grpc;
using API_Gateway.Interceptors;
using API_Gateway.Middlewares;
using API_Gateway.Models;
using API_Gateway.Redis;
using API_Gateway.Repository;
using API_Gateway.Services;
using ApiGateway.Protos;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
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
            // ── Redis Connection Multiplexer ─────────────────────────────────────────────────────────────
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var config = sp.GetRequiredService<IOptions<RedisConfigModel>>();

                ConfigurationOptions options = new()
                {
                    User = config.Value.Username,
                    Password = config.Value.Password,
                    AbortOnConnectFail = false
                };

                options.EndPoints.Add(
                    config.Value.GetRedisConnectionString());

                return ConnectionMultiplexer.Connect(options);
            });

            // ── Redis DB Reference ─────────────────────────────────────────────────────────────
            services.AddSingleton<IDatabase>(sp =>
            {
                return sp
                    .GetRequiredService<IConnectionMultiplexer>()
                    .GetDatabase();
            });

            // ── Redis Service ───────────────────────────────────────────────────────
            services.AddSingleton<IRedisService, RedisService>();

            // ── Filters ─────────────────────────────────────────────────────────────
            services.AddScoped<RequirePermissionFilter>();

            // ── Repository ──────────────────────────────────────────────────────────
            services.AddScoped<IRequestLogRepository, RequestLogRepository>();
            services.AddScoped<IAuthorizationHandler, RoleAuthorizationHandler>();

            // ── Main Auth Policy Provider
            services.AddSingleton<IAuthorizationPolicyProvider, RolePermissionPolicyProvider>();
            services.AddScoped<JwtForwardingInterceptor>();

            // ── Service ─────────────────────────────────────────────────────────────
            services.AddScoped<IRequestLogService, RequestLogService>();
            services.AddScoped<ITokenHelper, TokenHelper>();

            // ── External ─────────────────────────────────────────────────────────────
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IProductCategoryGrpcClient, ProductCategoryGrpcClient>();
            services.AddScoped<IProductGrpcClient, ProductGrpcClient>();
            services.AddScoped<ISellerGrpcClient, SellerGrpcClient>();
            services.AddScoped<IOrderGrpcClient, OrderGrpcClient>();
            services.AddScoped<ICustomerTransactionGrpcClient, CustomerTransactionGrpcClient>();
            services.AddScoped<IPermissionService, PermissionService>();
        }

        public static void RegisterMiddleware(this IServiceCollection services)
        {
            services.AddScoped<TokenAuthorizationMiddleware>();
        }

        public static void RegisterConfigOptions(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<MicroServiceUrl>(config.GetSection("MicroServiceUrls"));
            services.Configure<JwtInternalSchemaOptions>(config.GetSection(JwtInternalSchemaOptions.SectionName));
            services.Configure<RedisConfigModel>(config.GetSection(RedisConfigModel.SectionName));

        }

        public static void RegisterGrpcServices(this IServiceCollection services, IConfiguration config)
        {
            //load the microservice urls
            MicroServiceUrl serviceUrls = config.GetSection("MicroServiceUrls").Get<MicroServiceUrl>();

            //Register the services
            services.AddGrpcClient<User.UserClient>(options =>
            {
                options.Address = new Uri(serviceUrls.GetUserServiceUrl());
            }).AddInterceptor<JwtForwardingInterceptor>(InterceptorScope.Client);

            services.AddGrpcClient<Seller.SellerClient>(options =>
            {
                options.Address = new Uri(serviceUrls.GetProductServiceUrl());
            });

            services.AddGrpcClient<ProductService.ProductServiceClient>(options =>
            {
                options.Address = new Uri(serviceUrls.GetProductServiceUrl());
            }).AddInterceptor<JwtForwardingInterceptor>(InterceptorScope.Client);

            services.AddGrpcClient<ProductCategory.ProductCategoryClient>(options =>
            {
                options.Address = new Uri(serviceUrls.GetProductServiceUrl());
            });

            services.AddGrpcClient<OrderGrpcService.OrderGrpcServiceClient>(options =>
            {
                options.Address = new Uri(serviceUrls.GetOrderServiceUrl());
            });

            services.AddGrpcClient<Permission.PermissionClient>(options =>
            {
                options.Address = new Uri(serviceUrls.GetUserServiceUrl());
            });
        }
    }
}
