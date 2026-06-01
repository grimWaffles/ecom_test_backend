
using API_Gateway.Database;
using API_Gateway.Grpc;
using API_Gateway.Handlers;
using API_Gateway.Helpers;
using API_Gateway.Middlewares;
using API_Gateway.Models;
using API_Gateway.Repository;
using API_Gateway.Services;
using API_Gateway.Services.API_Gateway.Services;
using ApiGateway.Protos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrderServiceGrpc.Models.ConfigModels;
using StackExchange.Redis;
using System.Text;
namespace API_Gateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.Configure<DatabaseConfig>(builder.Configuration.GetSection("DatabaseConfig"));
            builder.Services.Configure<DatabaseConnection>(builder.Configuration.GetSection("ConnectionStrings"));

            //Configure the database context
            string dbType = builder.Configuration["DatabaseConfig:Database"] ?? "";
            string mode = builder.Configuration["DatabaseConfig:Mode"] ?? "";
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

            connectionString = builder.Configuration.GetConnectionString(dbKey) ?? "";

            if (connectionString == "")
            {
                throw new InvalidOperationException("Database connection string not found.");
            }

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString)
            );

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowOrigin", policy =>
                {
                    policy
                        .AllowAnyOrigin()   // allow requests from any origin
                        .AllowAnyMethod()   // allow all HTTP methods (GET, POST, PUT, DELETE, etc.)
                        .AllowAnyHeader();  // allow all headers
                });
            });

            builder.Services.AddHttpContextAccessor();

            //Add JWT Authentication
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters()
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["Jwt:validIssuer"],
                        ValidAudience = builder.Configuration["Jwt:validAudience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:signingKey"] ?? ""))
                    };
                });

            builder.Services.AddScoped<IAuthorizationHandler, RoleAuthorizationHandler>();
            builder.Services.AddScoped<IAuthorizationHandler, ReportAuthorizationHandler>();

            builder.Services.AddAuthentication();

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("RolePermissionPolicy", policy =>
                {
                    policy.AddRequirements(new RolePermissionRequirement());
                });

                options.AddPolicy("ReportResourcePolicy", policy =>
                {
                    policy.AddRequirements(new ReportResourceRequirement());
                });

                //Ensure all the endpoints require authorization by default
                //options.FallbackPolicy = options.GetPolicy("RolePermissionPolicy") ?? throw new InvalidOperationException("Fallback policy not found.");
            });

            builder.Services.AddControllers();

            DependencyResolver.RegisterMiddleware(builder.Services);
            DependencyResolver.RegisterServices(builder.Services, builder.Configuration);
            DependencyResolver.RegisterConfigOptions(builder.Services, builder.Configuration);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                //app.UseSwagger();
                //app.UseSwaggerUI();
            }

            app.UseCors("AllowOrigin");
            app.UseHttpsRedirection();

            //Use Custom Middlewares
            app.UseTokenAuthorizationMiddleware();
            app.UseRequestLogMiddleware();

            //Add Authentication and Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
