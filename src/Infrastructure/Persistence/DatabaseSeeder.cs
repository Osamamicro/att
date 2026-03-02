using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedDevelopmentDataAsync(IServiceProvider serviceProvider, string environmentName)
    {
        if (environmentName != "Development")
            return;

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Check if admin user already exists
            var adminEmail = "admin@admin.com";
            var existingAdmin = await context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

            if (existingAdmin == null)
            {
                // Create default admin user
                var adminUser = new User
                {
                    Email = adminEmail,
                    FirstName = "Admin",
                    LastName = "User",
                    Department = "Administration",
                    PersonNumber = "00000",
                    Role = "Admin",
                    IsActive = true,
                    PasswordHash = HashPassword("123456"),
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(adminUser);
                await context.SaveChangesAsync();

                logger.LogInformation("Development admin user created: {Email}", adminEmail);
                logger.LogInformation("Default credentials - Email: {Email}, Password: 123456", adminEmail);
            }
            else
            {
                var updated = false;
                var expectedHash = HashPassword("123456");

                if (string.IsNullOrWhiteSpace(existingAdmin.PasswordHash) || existingAdmin.PasswordHash != expectedHash)
                {
                    existingAdmin.PasswordHash = expectedHash;
                    updated = true;
                }

                if (!existingAdmin.IsActive)
                {
                    existingAdmin.IsActive = true;
                    updated = true;
                }

                if (!string.Equals(existingAdmin.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    existingAdmin.Role = "Admin";
                    updated = true;
                }

                if (updated)
                {
                    context.Users.Update(existingAdmin);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Development admin user updated: {Email}", adminEmail);
                    logger.LogInformation("Default credentials - Email: {Email}, Password: 123456", adminEmail);
                }

                logger.LogInformation("Development admin user already exists: {Email}", adminEmail);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
        }
    }

    private static string HashPassword(string password)
    {
        // Simple hash for development - use BCrypt in production
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
