using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using UserServiceGrpc.Database;
using UserServiceGrpc.Grpc;
using UserServiceGrpc.Helpers;
using UserServiceGrpc.Repository;
using UserServiceGrpc.Services;
namespace UserServiceGrpc
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddHttpContextAccessor();

            // Add services to the container.
            builder.Services.AddGrpc();

            //Configure the Database connection strings
            DependencyResolver.ConfigureDatabase(builder.Services, builder.Configuration);

            //Add services for dependency injection
            DependencyResolver.RegisterServices(builder.Services);
            DependencyResolver.RegisterConfigOptions(builder.Services, builder.Configuration);

            //Add Authentication and Authorization
            builder.Services.AddAuthentication(defaultScheme: "InternalAuthScheme")
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
            app.MapGrpcService<PermissionServiceGrpc>();

            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            //Load Permissions to cache
            DependencyResolver.LoadPermissionsToCache(app);

            app.Run();
        }
    }
}