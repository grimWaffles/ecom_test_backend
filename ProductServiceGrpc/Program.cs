using Microsoft.EntityFrameworkCore;
using ProductServiceGrpc.Database;
using ProductServiceGrpc.Repository;
using ProductServiceGrpc.Services;
using Microsoft.IdentityModel.Tokens;
using System.Text;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddGrpc();

        //Configure the database 
        ConfigureDatabase(builder.Services, builder.Configuration);

        //Add Dependency Injections
        builder.Services.AddScoped<ISellerRepository, SellerRepository>();
        builder.Services.AddScoped<IProductCategoryRepository, ProductCategoryRepository>();
        builder.Services.AddScoped<IProductRepository, ProductRepository>();

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

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();

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

        connectionString = configuration.GetConnectionString(dbKey) ?? "";

        if (connectionString == "")
        {
            throw new InvalidOperationException("Database connection string not found.");
        }

        services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString)
            );
    }
}