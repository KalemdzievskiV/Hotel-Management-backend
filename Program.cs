using HotelManagement.Configurations;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Filters;
using HotelManagement.Infrastructure.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Allow DateTime.Kind=Unspecified to be written to PostgreSQL timestamptz columns
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // Add global validation filter for consistent error responses
    options.Filters.Add<ValidationFilter>();
});

// Add all project-level dependencies (repositories, services, AutoMapper, DbContext, etc.)
builder.Services.AddProjectServices(builder.Configuration);

// Add CORS policy for frontend — origins read from config so they can be overridden via env vars
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Swagger for API testing with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your token in the text input below.\n\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\""
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed database with roles
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Auto-apply any pending EF Core migrations on startup
        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await DbSeeder.SeedRolesAsync(roleManager);
        
        // Create a default SuperAdmin user for testing
        var userManager = services.GetRequiredService<UserManager<HotelManagement.Models.Entities.ApplicationUser>>();
        await DbSeeder.SeedSuperAdminAsync(userManager);
        
        // Seed mock data for testing
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        await DbSeeder.SeedMockDataAsync(dbContext, userManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Global exception handling - must be first
app.UseExceptionHandling();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseRouting();
app.UseStaticFiles();

// Enable CORS
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// 3️⃣ Map API controllers
app.MapControllers();

// Health check endpoint for Railway
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Make the implicit Program class public for integration tests
public partial class Program { }