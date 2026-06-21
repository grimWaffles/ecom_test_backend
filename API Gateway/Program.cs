using API_Gateway.Database;
using API_Gateway.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace API_Gateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddMemoryCache();

            DependencyResolver.ConfigureDatabases(builder.Services, builder.Configuration);

            DependencyResolver.RegisterMiddleware(builder.Services);
            DependencyResolver.RegisterServices(builder.Services, builder.Configuration);
            DependencyResolver.RegisterConfigOptions(builder.Services, builder.Configuration);
            DependencyResolver.RegisterGrpcServices(builder.Services, builder.Configuration);

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

            //Rest of the policies are dynamically provided by the AuthProvider, only role-based are declared here.
            //Find the rest in ./AuthHandlers/PolicyProviders/RolePermissionPolicyProvider.cs
            builder.Services.AddAuthorization(
                options =>
                {
                    options.AddPolicy("AdminOnly", policy =>
                    {
                        policy.RequireRole("ADMIN");
                    });

                    options.AddPolicy("CustomerOnly", policy =>
                    {
                        policy.RequireRole("CUSTOMER");
                    });

                    options.AddPolicy("SellerOnly", policy =>
                    {
                        policy.RequireRole("SELLER");
                    });
                }
            );

            builder.Services.AddControllers();

            var app = builder.Build();

            app.UseCors("AllowOrigin");
            app.UseHttpsRedirection();

            //Use Custom Middleware
            //app.UseTokenAuthorizationMiddleware();
            //app.UseRequestLogMiddleware();

            //Use the configured Authentication and Authorization options
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
