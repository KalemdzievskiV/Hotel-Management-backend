using HotelManagement.Models.Constants;
using Microsoft.AspNetCore.Identity;

namespace HotelManagement.Data;

/// <summary>
/// Seeds initial data into the database
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// Seeds all roles defined in AppRoles
    /// </summary>
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in AppRoles.AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                
                if (result.Succeeded)
                {
                    Console.WriteLine($"✅ Role '{role}' created successfully");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to create role '{role}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                Console.WriteLine($"ℹ️  Role '{role}' already exists");
            }
        }
    }
    
    /// <summary>
    /// Optional: Seed a default SuperAdmin user
    /// </summary>
    public static async Task SeedSuperAdminAsync(
        UserManager<Models.Entities.ApplicationUser> userManager,
        string email = "superadmin@hotel.com",
        string password = "SuperAdmin123!",
        string fullName = "System Administrator")
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            Console.WriteLine($"ℹ️  SuperAdmin user already exists: {email}");
            return;
        }
        
        var superAdmin = new Models.Entities.ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Super",
            LastName = "Admin"
        };
        
        var result = await userManager.CreateAsync(superAdmin, password);
        
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(superAdmin, AppRoles.SuperAdmin);
            Console.WriteLine($"✅ SuperAdmin user created: {email}");
            Console.WriteLine($"   Password: {password}");
        }
        else
        {
            Console.WriteLine($"❌ Failed to create SuperAdmin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}
