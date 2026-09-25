using HotelManagement.Configurations;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Filters;
using HotelManagement.Infrastructure.Middleware;
using HotelManagement.Models.Constants;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Allow DateTime.Kind=Unspecified to be written to PostgreSQL timestamptz columns
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Override connection string from DATABASE_URL env var (Railway provides this automatically)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    // postgres://user:password@host:port/database - credentials may be URL-encoded
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var connectionString = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null
    };
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString.ConnectionString;
}

// Add services to the container.
builder.Services.AddControllers(options =>
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

// Apply migrations and seed. Failures here should stop startup rather than leave a half-initialized app.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    // Integration tests run on the in-memory provider, which doesn't support migrations
    if (context.Database.IsRelational())
        await context.Database.MigrateAsync();
    else
        await context.Database.EnsureCreatedAsync();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    await DbSeeder.SeedRolesAsync(roleManager);

    var userManager = services.GetRequiredService<UserManager<HotelManagement.Models.Entities.ApplicationUser>>();
    if (app.Environment.IsProduction())
    {
        // Never create the well-known default account in production; bootstrap only from explicit config
        var seedEmail = builder.Configuration["SeedAdmin:Email"];
        var seedPassword = builder.Configuration["SeedAdmin:Password"];
        if (!string.IsNullOrEmpty(seedEmail) && !string.IsNullOrEmpty(seedPassword))
            await DbSeeder.SeedSuperAdminAsync(userManager, seedEmail, seedPassword);
    }
    else
    {
        await DbSeeder.SeedSuperAdminAsync(userManager);
    }

    if (app.Environment.IsDevelopment())
        await DbSeeder.SeedMockDataAsync(context, userManager);

    // Hotel owners from before subscriptions existed start with a trial
    var entitlements = services.GetRequiredService<IEntitlementService>();
    foreach (var owner in await userManager.GetUsersInRoleAsync(AppRoles.Admin))
        await entitlements.EnsureSubscriptionAsync(owner.Id);
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

app.UseRouting();

// Enable CORS
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// The API has no UI of its own; in development the root opens the API docs
if (app.Environment.IsDevelopment())
    app.MapGet("/", () => Results.Redirect("/swagger"));

// Health check endpoint for Railway
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Make the implicit Program class public for integration tests
public partial class Program { }