using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var db = services.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        string[] roles = { "Admin", "Manager", "SalesExecutive" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        await SeedUser(userManager, "admin@acxiom.com", "Admin@123", "Admin User", "Admin");
        await SeedUser(userManager, "manager@acxiom.com", "Manager@123", "Manager User", "Manager");
        await SeedUser(userManager, "sales@acxiom.com", "Sales@123", "Sales Executive", "SalesExecutive");
    }

    private static async Task SeedUser(UserManager<AppUser> um, string email, string password, string name, string role)
    {
        if (await um.FindByEmailAsync(email) == null)
        {
            var user = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = name,
                IsActive = true,
                EmailConfirmed = true
            };
            var result = await um.CreateAsync(user, password);
            if (result.Succeeded)
                await um.AddToRoleAsync(user, role);
        }
    }
}

