using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderServiceGrpc.Authorization;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.GrpcServices;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Kafka.Consumers;
using OrderServiceGrpc.Kafka.Producers;
using OrderServiceGrpc.Models.ConfigModels;
using OrderServiceGrpc.Models.Configs;
using OrderServiceGrpc.Repository;
using OrderServiceGrpc.Services;
using OrderServiceGrpc.Services.BackgroundServices;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<RedisConfigModel>(builder.Configuration.GetSection(RedisConfigModel.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddGrpc();

//Configure Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IOptions<RedisConfigModel>>();

    ConfigurationOptions options = new()
    {
        User = config.Value.Username,
        Password = config.Value.Password,
        AbortOnConnectFail = false
    };

    options.EndPoints.Add(
        config.Value.GetRedisConnectionString());

    return ConnectionMultiplexer.Connect(options);
});

// ── Redis DB Reference ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDatabase>(sp =>
{
    return sp
        .GetRequiredService<IConnectionMultiplexer>()
        .GetDatabase();
});

builder.Services.AddSingleton<IRedisService, RedisService>();

//Add the configurations from appsettings.json 
builder.Services.Configure<DatabaseConfig>(builder.Configuration.GetSection("DatabaseConfig"));
builder.Services.Configure<DatabaseConnection>(builder.Configuration.GetSection("ConnectionStrings"));

builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("KafkaGlobalSettings"));

builder.Services.Configure<KafkaConsumerSettings>(builder.Configuration.GetSection("KafkaConsumerSettings"));
builder.Services.Configure<KafkaProducerSettings>(builder.Configuration.GetSection("KafkaProducerSettings"));

builder.Services.Configure<OrderEventConsumerSettings>(builder.Configuration.GetSection("OrderEventConsumerSettings"));
builder.Services.Configure<TransactionEventConsumerSettings>(builder.Configuration.GetSection("TransactionEventConsumerSettings"));

//Add Helper Services 
builder.Services.AddScoped<ITokenHelper, TokenHelper>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

builder.Services.AddSingleton<IAuthorizationPolicyProvider, AuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, RolePermissionHandler>();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtInternalSchema:SigningKey"] ?? ""))
        };
    });

builder.Services.AddAuthorization();


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

//Dependency Injection
builder.Services.AddSingleton<IKafkaEventProducer, KafkaEventProducer>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<UnitOfWorkContext>();

builder.Services.AddScoped<ICustomerTransactionRepository, CustomerTransactionRepository>();
builder.Services.AddScoped<ICustomerTransactionService, CustomerTransactionService>();

builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.AddScoped<IOrderOutboxService, OrderOutboxService>();
builder.Services.AddScoped<IOrderOutboxRepository, OrderOutboxRepository>();

builder.Services.AddScoped<IOutboxStatusService, OutboxStatusService>();
builder.Services.AddScoped<IOutboxStatusRepository, OutboxStatusRepository>();

builder.Services.AddHostedService<OutboxExecutor>();
builder.Services.AddHostedService<OrderEventConsumer>();
builder.Services.AddHostedService<TransactionEventConsumer>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
app.MapGrpcService<CustomerTransactionGrpcService>();
app.MapGrpcService<OrderGrpcService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
