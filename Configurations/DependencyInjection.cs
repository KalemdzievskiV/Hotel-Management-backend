using System.Text;
using AutoMapper;
using FluentValidation;
using FluentValidation.AspNetCore;
using HotelManagement.Data;
using HotelManagement.Models;
using HotelManagement.Repositories.Interfaces;
using HotelManagement.Repositories.Implementations;
using HotelManagement.Services.Interfaces;
using HotelManagement.Services.Implementations;
using HotelManagement.Infrastructure.Mapping;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Authorization.Handlers;
using HotelManagement.Authorization.Requirements;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace HotelManagement.Configurations
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddProjectServices(this IServiceCollection services, IConfiguration configuration)
        {
            // 1️⃣ HTTP Context Accessor (for getting current user in services)
            services.AddHttpContextAccessor();

            // 2️⃣ Database (adjust connection string name as needed)
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            // 3️⃣ Repositories
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            // 4️⃣ Services
            // 3️⃣ Services
            services.AddScoped<ICrudService<HotelDto>>(sp =>
            {
                var repo = sp.GetRequiredService<IGenericRepository<Hotel>>();
                var mapper = sp.GetRequiredService<IMapper>();
                return new CrudService<Hotel, HotelDto>(repo, mapper);
            });
            
            services.AddScoped<IHotelService, HotelService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<IGuestService, GuestService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IReservationService, ReservationService>();
            services.AddScoped<ITokenService, TokenService>();

            // 4️⃣ AutoMapper
            services.AddAutoMapper(typeof(AutoMapperProfile));

            // 5️⃣ Identity
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // 6️⃣ JWT Configuration
            var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>();
            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings!.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
                };
            });

            // 7️⃣ Token Service
            services.AddScoped<ITokenService, TokenService>();

            // 8️⃣ Business Services
            services.AddScoped<IHotelService, HotelService>();
            services.AddScoped<IReportService, ReportService>();

            // 9️⃣ FluentValidation
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<Program>();

            // 🔟 Authorization Policies and Handlers
            services.AddAuthorization(options =>
            {
                // Hotel management policies
                options.AddPolicy("CanViewHotel", policy =>
                    policy.Requirements.Add(new HotelOwnershipRequirement()));

                options.AddPolicy("CanManageHotel", policy =>
                    policy.Requirements.Add(new ManageHotelRequirement()));

                // Reservation access policy
                options.AddPolicy("CanAccessReservation", policy =>
                    policy.Requirements.Add(new ReservationAccessRequirement()));

                // Role-based policies for quick checks
                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireRole("SuperAdmin", "Admin"));

                options.AddPolicy("ManagerOrAbove", policy =>
                    policy.RequireRole("SuperAdmin", "Admin", "Manager"));
            });

            // Register authorization handlers
            services.AddScoped<IAuthorizationHandler, HotelOwnershipHandler>();
            services.AddScoped<IAuthorizationHandler, HotelOwnershipByIdHandler>();
            services.AddScoped<IAuthorizationHandler, ManageHotelHandler>();
            services.AddScoped<IAuthorizationHandler, CreateHotelHandler>();
            services.AddScoped<IAuthorizationHandler, ReservationAccessHandler>();
            services.AddScoped<IAuthorizationHandler, ReservationAccessByIdHandler>();

            return services;
        }
    }
}