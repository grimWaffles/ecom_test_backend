using Microsoft.EntityFrameworkCore;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Kafka;
using OrderServiceGrpc.Models.ConfigModels;
using OrderServiceGrpc.Models.Configs;
using OrderServiceGrpc.Repository;
using OrderServiceGrpc.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

//Add the configurations from appsettings.json 
builder.Services.Configure<DatabaseConfig>(builder.Configuration.GetSection("DatabaseConfig"));
builder.Services.Configure<DatabaseConnection>(builder.Configuration.GetSection("ConnectionStrings"));

builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("KafkaGlobalSettings"));

builder.Services.Configure<KafkaConsumerSettings>(builder.Configuration.GetSection("KafkaConsumerSettings"));
builder.Services.Configure<KafkaProducerSettings>(builder.Configuration.GetSection("KafkaProducerSettings"));

builder.Services.Configure<OrderEventConsumerSettings>(builder.Configuration.GetSection("OrderEventConsumerSettings"));
builder.Services.Configure<TransactionEventConsumerSettings>(builder.Configuration.GetSection("TransactionEventConsumerSettings"));

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
builder.Services.AddScoped<ICustomerTransactionProcessorService, CustomerTransactionProcessorService>();

builder.Services.AddScoped<IOrderProcessorService, OrderProcessorService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.AddScoped<IOrderOutboxService, OrderOutboxService>();
builder.Services.AddScoped<IOrderOutboxRepository, OrderOutboxRepository>();
builder.Services.AddScoped<IOutboxStatusService, OutboxStatusService>();
builder.Services.AddScoped<IOutboxStatusRepository, OutboxStatusRepository>();

//builder.Services.AddHostedService<OrderEventConsumer>();
//builder.Services.AddHostedService<TransactionEventConsumer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<CustomerTransactionGrpcService>();
app.MapGrpcService<OrderGrpcService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
