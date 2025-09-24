using OrderServiceGrpc.Repository;
using OrderServiceGrpc.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

//Dependency Injection
builder.Services.AddScoped<ICustomerTransactionRepository, CustomerTransactionRepository>();
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();

builder.Services.AddHostedService<OrderEventConsumer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<CustomerTransactionGrpcService>();
app.MapGrpcService<OrderService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
