using Microsoft.EntityFrameworkCore;
using ProductServiceGrpc.Database;
using ProductServiceGrpc.Repository;
using ProductServiceGrpc.Services;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddGrpc();

        //Configure the database 
        ConfigureDatabase(builder.Services,builder.Configuration);

        //Add Database to the server
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        );

        //Add Dependency Injections
        builder.Services.AddScoped<ISellerRepository, SellerRepository>();
        builder.Services.AddScoped<IProductCategoryRepository, ProductCategoryRepository>();
        builder.Services.AddScoped<IProductRepository, ProductRepository>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        app.MapGrpcService<ProductService>();
        app.MapGrpcService<ProductCategoryService>();
        app.MapGrpcService<SellerService>();

        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        app.Run();
    }

    static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration)
    {
        string dbType = configuration["DatabaseConfig:Database"] ?? "";
        string mode = configuration["DatabaseConfig:Mode"] ?? "";
        string dbKey = "";
        string connectionString = "";

        if (dbType == "" || mode == "")
        {
            throw new InvalidOperationException("Database configuration not set up correctly.");
        }

        dbKey = (dbType.ToLower(), mode.ToLower()) switch
        {
            ("mysql", "local") => "MySqlConnection",
            ("mysql", "docker") => "MySqlDockerConnection",
            ("sqlserver", "local") => "SqlServerConnection",
            ("sqlserver", "docker") => "SqlServerDockerConnection",
            _ => ""
        };

        if (dbKey == "")
        {
            throw new InvalidOperationException("Database key not found.");
        }

        connectionString = configuration.GetConnectionString(dbKey) ?? "";

        if (connectionString == "")
        {
            throw new InvalidOperationException("Database connection string not found.");
        }

        if (dbType == "mysql")
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseMySQL(connectionString)
            );
        }
        else if (dbType == "sqlserver")
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString)
            );
        }
    }
}