using OrderServiceGrpc.Kafka;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Repository;
using OrderServiceGrpc.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

//Add the configurations from appsettings.json 
builder.Services.Configure<DatabaseConfig>(builder.Configuration.GetSection("DatabaseConfig"));
builder.Services.Configure<DatabaseConnection>(builder.Configuration.GetSection("ConnectionStrings"));
builder.Services.Configure<KafkaConsumerSettings>(builder.Configuration.GetSection("KafkaConsumerSettings"));

//Dependency Injection
builder.Services.AddScoped<ICustomerTransactionRepository, CustomerTransactionRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.AddHostedService<KafkaEventConsumer>();


var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<CustomerTransactionGrpcService>();
app.MapGrpcService<OrderService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
