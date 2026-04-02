
using API_Gateway.Helpers;
using API_Gateway.Kafka;
using API_Gateway.Middlewares;
using API_Gateway.Models;
using API_Gateway.Services;
using ApiGateway.Protos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
namespace API_Gateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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

            //Add JWT Authentication
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => {
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
            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();

            builder.Services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            //builder.Services.AddEndpointsApiExplorer();
            //builder.Services.AddSwaggerGen();

            //Add Dependency Injection
            builder.Services.AddScoped<IUserServiceClient, UserServiceClient>();
            builder.Services.AddScoped<IProductCategoryGrpcClient,ProductCategoryGrpcClient>();
            builder.Services.AddScoped<IProductGrpcClient, ProductGrpcClient>();
            builder.Services.AddScoped<ISellerGrpcClient, SellerGrpcClient>();
            builder.Services.AddScoped<IOrderGrpcClient, OrderGrpcClient>();

            builder.Services.AddSingleton<IKafkaEventProducer, KafkaEventProducer>();
            builder.Services.AddSingleton<IRedisService, RedisService>();

            //Add AppSettings objects as Options
            builder.Services.Configure<KafkaProducerSettings>(builder.Configuration.GetSection("KafkaProducerSettings"));
            builder.Services.Configure<KafkaGlobalSetting>(builder.Configuration.GetSection("Kafka"));
            builder.Services.Configure<MicroServiceUrl>(builder.Configuration.GetSection("MicroServiceUrls"));

            builder.Services.AddScoped<TokenAuthorizationMiddleware>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                //app.UseSwagger();
                //app.UseSwaggerUI();
            }

            app.UseCors("AllowOrigin");
            app.UseHttpsRedirection();

            app.UseTokenAuthorizationMiddleware();

            //Add Authentication and Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
