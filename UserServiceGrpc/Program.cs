using Microsoft.EntityFrameworkCore;
using UserServiceGrpc.Database;
using UserServiceGrpc.Repository;
using UserServiceGrpc.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
namespace UserServiceGrpc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddGrpc();

            ConfigureDatabase(builder.Services, builder.Configuration);

            //Add JWT Auth
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options=>{
                    options.TokenValidationParameters = new TokenValidationParameters(){
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

            //Add services for dependency injection
            builder.Services.AddScoped<IUserRepository, UserRepository>();

            var app = builder.Build();

            //Add Authentication and Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            // Configure the HTTP request pipeline.
            app.MapGrpcService<UserService>();

            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            app.Run();
        }

        static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration){
            string dbType = configuration["DatabaseConfig:Database"] ?? "";
            string mode = configuration["DatabaseConfig:Mode"] ?? "";
            string dbKey = "";
            string connectionString = "";

            if(dbType=="" || mode == "")
            {
                throw new InvalidOperationException("Database configuration not set up correctly.");
            }

            dbKey = (dbType.ToLower(),mode.ToLower()) switch
            {
                ("mysql","local")=>"MySqlConnection",
                ("mysql","docker")=>"MySqlDockerConnection",
                ("sqlserver","local")=>"SqlServerConnection",
                ("sqlserver","docker")=>"SqlServerDockerConnection",
                _=>""
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
                services.AddDbContext<AppDbContext>(options=>
                    options.UseMySQL(connectionString)
                );
            }
            else if (dbType == "sqlserver")
            {
                services.AddDbContext<AppDbContext>(options=>
                    options.UseSqlServer(connectionString)
                );
            }
        }
    }
}