using HotelManagement.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HotelManagement.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory that uses in-memory database for testing
/// This ensures tests don't pollute the real database
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Directory.GetCurrentDirectory());
        // Not "Development", so the mock demo data isn't seeded into test databases
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // Remove all existing DbContext registrations
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions>();
            // EF Core 9 keeps provider configuration here too; leaving it registers Npgsql alongside InMemory
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            
            // Add in-memory database for testing with unique name per test run
            var databaseName = "TestDatabase_" + Guid.NewGuid().ToString();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName);
            });
        });
    }
}
