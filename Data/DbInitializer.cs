using JwtAuthApi_Sonnet45.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JwtAuthApi_Sonnet45.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Seed Roles
        if (!await context.Roles.AnyAsync())
        {
            var roles = new[]
            {
                new Role { Name = "Admin", Description = "Administrator with full access" },
                new Role { Name = "User", Description = "Standard user with limited access" },
                new Role { Name = "Moderator", Description = "Moderator with elevated privileges" }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        // Seed Admin User
        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            
            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = "salt:hash", // Will be replaced by actual password hasher
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();

            if (adminRole != null)
            {
                await context.UserRoles.AddAsync(new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id,
                    AssignedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
            }
        }
    }
}