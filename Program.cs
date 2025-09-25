using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using ShopLedger.Data;
using ShopLedger.Data.Models;
using ShopLedger.Data.Utils;
using System.Text;

namespace ShopLedger
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ===== DbContext =====
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // ===== Identity (”Ã¯· „—… Ê«Õœ… ›ﬁÿ) =====
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
            {
                opt.User.AllowedUserNameCharacters += " "; // «·”„«Õ »«·„”«›… ›Ì «”„ «·„” Œœ„
                opt.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // ===== Controllers + OpenAPI =====
            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            // ===== JWT Auth =====
            var jwtKey = builder.Configuration["jwtOptions:SecretKey"];
            if (string.IsNullOrWhiteSpace(jwtKey))
                throw new InvalidOperationException("Missing jwtOptions:SecretKey in appsettings.json");

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });

            builder.Services.AddAuthorization();

            // ===== Seed =====
            builder.Services.AddScoped<SeedData>();

            var app = builder.Build();

            // Run seed (migrate + users/roles)
            using (var scope = app.Services.CreateScope())
            {
                var seeder = scope.ServiceProvider.GetRequiredService<SeedData>();
                await seeder.IdentityDataSeedingAsync();
            }

            // ===== Pipeline =====
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();  
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
