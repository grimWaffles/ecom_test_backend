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

//Dependency Injection
builder.Services.AddScoped<ICustomerTransactionRepository, CustomerTransactionRepository>();
builder.Services.AddScoped<ICustomerTransactionProcessorService, CustomerTransactionProcessorService>();

builder.Services.AddScoped<IOrderProcessorService, OrderProcessorService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.AddSingleton<KafkaEventProducer>();

// builder.Services.AddHostedService<OrderEventConsumer>();
// builder.Services.AddHostedService<TransactionEventConsumer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<CustomerTransactionGrpcService>();
app.MapGrpcService<OrderGrpcService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
