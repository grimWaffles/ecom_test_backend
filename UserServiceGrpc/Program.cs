using Microsoft.EntityFrameworkCore;
using UserServiceGrpc.Database;
using UserServiceGrpc.Repository;
using UserServiceGrpc.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
namespace UserServiceGrpc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddGrpc();

            ConfigureDatabase(builder.Services, builder.Configuration);

            //Add services for dependency injection
            builder.Services.AddScoped<IUserRepository, UserRepository>();

            builder.Services.AddScoped<IRoleRepository, RoleRepository>();
            builder.Services.AddScoped<IRoleService, RoleService>();

            builder.Services.AddScoped<ISecurityPermissionRepository, SecurityPermissionRepository>();
            builder.Services.AddScoped<ISecurityPermissionService, SecurityPermissionService>();

            builder.Services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
            builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();

            //Add Authentication and Authorization
            builder.Services.AddAuthentication(defaultScheme: "UserAuthScheme")
                .AddJwtBearer("UserAuthScheme", options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters()
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer = builder.Configuration["JwtUserSchema:validIssuer"],
                        ValidAudience = builder.Configuration["JwtUserSchema:validAudience"],
                        RoleClaimType = "Role",
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtUserSchema:SigningKey"]))
                    };
                })
                .AddJwtBearer("InternalAuthScheme", options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters()
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer = builder.Configuration["JwtInternalSchema:validIssuer"],
                        ValidAudience = builder.Configuration["JwtInternalSchema:validAudience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtInternalSchema:SigningKey"]))
                    };
                });

            builder.Services.AddAuthorization();

            var app = builder.Build();

            app.UseAuthentication();
            app.UseAuthorization();

            // Configure the HTTP request pipeline.
            app.MapGrpcService<UserGrpcService>();

            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            //Call seeder to populate permissions data
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                RolePermissionSeeder seeder = new RolePermissionSeeder(dbContext);

                seeder.SeedRolePermissions();
            }

            app.Run();
        }

        static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration)
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