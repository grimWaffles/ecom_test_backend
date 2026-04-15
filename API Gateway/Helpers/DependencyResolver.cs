using API_Gateway.Grpc;
using API_Gateway.Middlewares;
using API_Gateway.Models;
using API_Gateway.Services;
using API_Gateway.Services.API_Gateway.Services;
using System.Runtime.CompilerServices;

namespace API_Gateway.Helpers
{
    public static class DependencyResolver
    {
        public static void RegisterServices(this IServiceCollection services, IConfiguration config)
        {
            //Add Dependency Injection
            services.AddScoped<IUserGrpcClient, UserGrpcClient>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IProductCategoryGrpcClient, ProductCategoryGrpcClient>();
            services.AddScoped<IProductGrpcClient, ProductGrpcClient>();
            services.AddScoped<ISellerGrpcClient, SellerGrpcClient>();
            services.AddScoped<IOrderGrpcClient, OrderGrpcClient>();

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
