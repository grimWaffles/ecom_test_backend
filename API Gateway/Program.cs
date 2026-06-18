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

            DependencyResolver.ConfigureDatabases(builder.Services, builder.Configuration);
            DependencyResolver.RegisterMiddleware(builder.Services);
            DependencyResolver.RegisterServices(builder.Services, builder.Configuration);
            DependencyResolver.RegisterConfigOptions(builder.Services, builder.Configuration);
            DependencyResolver.RegisterGrpcServices(builder.Services,builder.Configuration);

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
                        RoleClaimType = "Role",
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SigningKey"]))
                    };
                });

            //Rest are provided by the AuthProvider, only role-based are declared here.
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

            //Use Custom Middlewares
            //app.UseTokenAuthorizationMiddleware();
            //app.UseRequestLogMiddleware();

            //Add Authentication and Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
