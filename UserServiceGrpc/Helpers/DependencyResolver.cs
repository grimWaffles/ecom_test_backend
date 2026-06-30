using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using UserServiceGrpc.Authorization;
using UserServiceGrpc.Database;
using UserServiceGrpc.Models;
using UserServiceGrpc.Repository;
using UserServiceGrpc.Services;

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

        public static async Task LoadPermissionsToCache(this WebApplication? app)
        {
            try
            {
                using (var scope = app.Services.CreateAsyncScope())
                {
                    //Load Permissions data
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    RolePermissionSeeder seeder = new RolePermissionSeeder(dbContext);

                    seeder.SeedRolePermissions();

                    //Load to cache
                    IRolePermissionService rolePermissionService = scope.ServiceProvider.GetRequiredService<IRolePermissionService>();

                    List<RolePermissionDto> dataToLoad = await rolePermissionService.GetAllPermissionsByRoleId(1);

                    Dictionary<string, string> formattedDictionary = await rolePermissionService.GetPermissionListDictionary(dataToLoad);

                    IRedisService redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();
                    
                    int statusCount = 0;

                    foreach (var (key,value) in formattedDictionary)
                    {
                        bool r = redisService.SetValueByKey(key, value);

                        if (r)
                        {
                            statusCount++;
                        }
                    }

                    Console.WriteLine("Data to loaded to cache: " + statusCount.ToString());
                }
            }
            catch
            {
                Console.WriteLine("Failed to preload cache");
            }
        }
    }
}
