using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using UserServiceGrpc.Authorization;
using UserServiceGrpc.Database;
using UserServiceGrpc.Models;
using UserServiceGrpc.Models.RedisModels;
using UserServiceGrpc.Repository;
using UserServiceGrpc.Services;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace UserServiceGrpc.Helpers
{
    public static class DependencyResolver
    {
        public static void RegisterConfigOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<JwtUserSchemaOptions>(configuration.GetSection(JwtUserSchemaOptions.SectionName));
            services.Configure<RedisConfigModel>(configuration.GetSection(RedisConfigModel.SectionName));
        }

        public static void RegisterServices(this IServiceCollection services)
        {
            services.AddSingleton<IRedisService, RedisService>();

            services.AddScoped<ITokenHelper, TokenHelper>();

            services.AddSingleton<IAuthorizationPolicyProvider, AuthorizationPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, RolePermissionHandler>();

            services.AddScoped<IUserRepository, UserRepository>();

            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IRoleService, RoleService>();

            services.AddScoped<ISecurityPermissionRepository, SecurityPermissionRepository>();
            services.AddScoped<ISecurityPermissionService, SecurityPermissionService>();

            services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
            services.AddScoped<IRolePermissionService, RolePermissionService>();

            services.AddScoped<IUserService, UserService>();
        }

        public static void ConfigureDatabase(this IServiceCollection services, IConfiguration configuration)
        {
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
    }
}
