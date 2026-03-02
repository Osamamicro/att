using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Infrastructure.Persistence
{
    /// <summary>
    /// Factory for creating DbContext instances at design-time for Entity Framework Core migrations.
    /// This is required because migrations need to instantiate DbContext without the full dependency injection container.
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // When running from the main application project directory (Dashboard),
            // the current directory is already at the project root
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
                .Build();

            // Get the connection string
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json");
            }

            // Create DbContextOptions
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
