
using API_Gateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ApiGateway.Protos;
using System.Text;
using StackExchange.Redis;
using API_Gateway.Helpers;
namespace API_Gateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            //Add CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowOrigin", builder =>
                {
                    builder.WithOrigins().AllowAnyMethod()
                        .AllowAnyHeader()
                        .SetIsOriginAllowed(origin => true) // allow any origin

                        .Build();
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
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:signingKey"]))
                    };
                });
            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();

            builder.Services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            //Add Dependency Injection
            builder.Services.AddScoped<IUserServiceClient, UserServiceClient>();
            builder.Services.AddScoped<IProductCategoryGrpcClient,ProductCategoryGrpcClient>();
            builder.Services.AddScoped<IProductGrpcClient, ProductGrpcClient>();
            builder.Services.AddScoped<ISellerGrpcClient, SellerGrpcClient>();
            builder.Services.AddScoped<IOrderGrpcClient, OrderGrpcClient>();

            builder.Services.AddSingleton<IKafkaEventProducer, KafkaEventProducer>();

            builder.Services.AddSingleton<IRedisService, RedisService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors("AllowOrigin");
            app.UseHttpsRedirection();

            //Add Authentication and Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
