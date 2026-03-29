using HotelManagement.Models.Constants;
using HotelManagement.Models.Enums;
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

    /// <summary>
    /// Seeds mock data for testing: Admin user, Manager user, Hotel, Rooms, Guests, and Reservations
    /// </summary>
    public static async Task SeedMockDataAsync(
        ApplicationDbContext context,
        UserManager<Models.Entities.ApplicationUser> userManager)
    {
        Console.WriteLine("🌱 Starting mock data seeding...");

        // 1. Create Admin User
        var adminEmail = "admin@hotel.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new Models.Entities.ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "John",
                LastName = "Smith"
            };

            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, AppRoles.Admin);
                Console.WriteLine($"✅ Admin user created: {adminEmail}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to create Admin user");
                return;
            }
        }

        // 2. Create Hotel (fix: OwnerId must be set)
        var hotel = context.Hotels.FirstOrDefault();
        if (hotel == null)
        {
            hotel = new Models.Entities.Hotel
            {
                OwnerId = adminUser.Id,
                Name = "Grand Paradise Hotel",
                Description = "A luxury beachfront hotel with stunning ocean views and world-class amenities.",
                Address = "123 Ocean Drive",
                City = "Miami",
                Country = "USA",
                PostalCode = "33139",
                PhoneNumber = "+1-305-555-0123",
                Email = "info@grandparadise.com",
                Website = "https://grandparadise.com",
                Stars = 4,
                CheckInTime = "14:00",
                CheckOutTime = "11:00",
                BufferTimeHours = 3,
                Amenities = "WiFi,Parking,Pool,Gym,Restaurant,Bar,Spa,BeachAccess",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            };
            context.Hotels.Add(hotel);
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ Hotel created: {hotel.Name}");

            adminUser.HotelId = hotel.Id;
            await userManager.UpdateAsync(adminUser);
        }

        // 3. Create Manager User (for role testing)
        var managerEmail = "manager@hotel.com";
        var managerUser = await userManager.FindByEmailAsync(managerEmail);
        if (managerUser == null)
        {
            managerUser = new Models.Entities.ApplicationUser
            {
                UserName = managerEmail,
                Email = managerEmail,
                EmailConfirmed = true,
                FirstName = "Sarah",
                LastName = "Johnson",
                HotelId = hotel.Id,
                JobTitle = "Front Desk Manager"
            };
            var result = await userManager.CreateAsync(managerUser, "Manager123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(managerUser, AppRoles.Manager);
                Console.WriteLine($"✅ Manager user created: {managerEmail}");
            }
        }

        // 4. Create Rooms (varied types across 3 floors)
        if (!context.Rooms.Any())
        {
            var roomDefinitions = new[]
            {
                // Floor 1 - Standard rooms
                ("101", RoomType.Single,    1,  80m,   false, null as decimal?),
                ("102", RoomType.Single,    1,  80m,   false, null),
                ("103", RoomType.Double,    2,  120m,  false, null),
                ("104", RoomType.Double,    2,  120m,  false, null),
                ("105", RoomType.Twin,      2,  110m,  false, null),
                ("106", RoomType.Twin,      2,  110m,  false, null),
                ("107", RoomType.Family,    4,  180m,  false, null),
                // Floor 2 - Deluxe rooms (allow short stay)
                ("201", RoomType.Deluxe,    2,  200m,  true,  35m),
                ("202", RoomType.Deluxe,    2,  200m,  true,  35m),
                ("203", RoomType.Deluxe,    2,  200m,  true,  35m),
                ("204", RoomType.Triple,    3,  160m,  false, null),
                ("205", RoomType.Triple,    3,  160m,  false, null),
                ("206", RoomType.Studio,    2,  150m,  true,  30m),
                ("207", RoomType.Accessible,2,  110m,  false, null),
                // Floor 3 - Suites
                ("301", RoomType.Suite,     4,  350m,  false, null),
                ("302", RoomType.Suite,     4,  350m,  false, null),
                ("303", RoomType.Suite,     2,  380m,  false, null),
                ("304", RoomType.Presidential, 6, 700m, false, null),
            };

            foreach (var (number, type, capacity, price, shortStay, hourlyRate) in roomDefinitions)
            {
                var floor = int.Parse(number.Substring(0, 1));
                context.Rooms.Add(new Models.Entities.Room
                {
                    HotelId = hotel.Id,
                    RoomNumber = number,
                    Type = type,
                    Floor = floor,
                    Capacity = capacity,
                    PricePerNight = price,
                    AllowsShortStay = shortStay,
                    ShortStayHourlyRate = hourlyRate,
                    MinimumShortStayHours = shortStay ? 2 : null,
                    MaximumShortStayHours = shortStay ? 12 : null,
                    Status = RoomStatus.Available,
                    IsActive = true,
                    BedType = type switch
                    {
                        RoomType.Single => "1 Single Bed",
                        RoomType.Double => "1 Queen Bed",
                        RoomType.Twin => "2 Single Beds",
                        RoomType.Triple => "3 Single Beds",
                        RoomType.Suite => "1 King Bed",
                        RoomType.Deluxe => "1 King Bed",
                        RoomType.Presidential => "1 King Bed + Sofa Bed",
                        RoomType.Studio => "1 Double Bed",
                        RoomType.Family => "1 King Bed + 2 Single Beds",
                        RoomType.Accessible => "1 Queen Bed",
                        _ => "1 Double Bed"
                    },
                    Description = $"{type} room on floor {floor} with modern amenities.",
                    Amenities = "WiFi,TV,AC,Minibar,SafeBox",
                    HasBalcony = floor == 3,
                    HasBathtub = type == RoomType.Suite || type == RoomType.Presidential,
                    ViewType = floor == 3 ? "Ocean View" : floor == 2 ? "Pool View" : "City View",
                    CreatedAt = DateTime.UtcNow.AddMonths(-6)
                });
            }
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ Created {roomDefinitions.Length} rooms across 3 floors");
        }

        // 5. Create Guests
        if (!context.Guests.Any())
        {
            var firstNames = new[] { "Emma", "Liam", "Olivia", "Noah", "Ava", "Elijah", "Sophia", "James", "Isabella", "Oliver",
                                     "Charlotte", "Benjamin", "Amelia", "Lucas", "Mia", "Henry", "Harper", "Alexander", "Evelyn", "Sebastian",
                                     "Victoria", "Daniel", "Grace", "Michael", "Chloe", "Ethan", "Lily", "Matthew", "Zoe", "David" };
            var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
                                    "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
                                    "White", "Harris", "Thompson", "Clark", "Lewis", "Robinson", "Walker", "Hall", "Allen", "Young" };
            var countries = new[] { "USA", "UK", "Germany", "France", "Canada", "Australia", "Spain", "Italy", "Netherlands", "Sweden" };
            var random = new Random(42);

            for (int i = 0; i < 60; i++)
            {
                var firstName = firstNames[random.Next(firstNames.Length)];
                var lastName = lastNames[random.Next(lastNames.Length)];
                var createdDaysAgo = random.Next(10, 365);

                context.Guests.Add(new Models.Entities.Guest
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = $"{firstName.ToLower()}.{lastName.ToLower()}{i}@email.com",
                    PhoneNumber = $"+1-{random.Next(200, 999)}-{random.Next(100, 999)}-{random.Next(1000, 9999)}",
                    Country = countries[random.Next(countries.Length)],
                    Nationality = countries[random.Next(countries.Length)],
                    HotelId = hotel.Id,
                    CreatedByUserId = adminUser.Id,
                    IsVIP = i < 5,
                    Notes = i < 3 ? "Regular guest, prefers high floor" : null,
                    CreatedAt = DateTime.UtcNow.AddDays(-createdDaysAgo)
                });
            }
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ Created 60 guests");
        }

        // 6. Create Reservations (spread over 6 months for analytics)
        if (!context.Reservations.Any())
        {
            var rooms = context.Rooms.ToList();
            var guests = context.Guests.ToList();
            var random = new Random(42);
            var paymentMethods = new[] { PaymentMethod.Cash, PaymentMethod.CreditCard, PaymentMethod.DebitCard, PaymentMethod.BankTransfer };

            var reservations = new List<Models.Entities.Reservation>();

            // Historical reservations — past 6 months (for analytics/reports)
            for (int i = 0; i < 150; i++)
            {
                var room = rooms[random.Next(rooms.Count)];
                var guest = guests[random.Next(guests.Count)];
                var daysAgo = random.Next(2, 180);
                var checkInDate = DateTime.UtcNow.AddDays(-daysAgo);
                var nights = random.Next(1, 8);
                var checkOutDate = checkInDate.AddDays(nights);
                var paymentMethod = paymentMethods[random.Next(paymentMethods.Length)];
                var totalAmount = Math.Round(room.PricePerNight * nights, 2);

                // Mostly checked out, some cancelled, some no-show
                var roll = random.Next(100);
                var status = roll < 75 ? ReservationStatus.CheckedOut
                           : roll < 88 ? ReservationStatus.Cancelled
                           : ReservationStatus.NoShow;

                reservations.Add(new Models.Entities.Reservation
                {
                    HotelId = hotel.Id,
                    RoomId = room.Id,
                    GuestId = guest.Id,
                    CreatedByUserId = adminUser.Id,
                    BookingType = BookingType.Daily,
                    CheckInDate = checkInDate,
                    CheckOutDate = checkOutDate,
                    NumberOfGuests = random.Next(1, room.Capacity + 1),
                    Status = status,
                    PaymentStatus = status == ReservationStatus.CheckedOut ? PaymentStatus.Paid : PaymentStatus.Unpaid,
                    PaymentMethod = status == ReservationStatus.CheckedOut ? paymentMethod : null,
                    TotalAmount = totalAmount,
                    DepositAmount = status == ReservationStatus.CheckedOut ? totalAmount : 0,
                    RemainingAmount = 0,
                    CancellationReason = status == ReservationStatus.Cancelled ? "Guest request" : null,
                    CancelledAt = status == ReservationStatus.Cancelled ? checkInDate.AddDays(-1) : null,
                    CheckedInAt = status == ReservationStatus.CheckedOut || status == ReservationStatus.CheckedIn ? checkInDate : null,
                    CheckedOutAt = status == ReservationStatus.CheckedOut ? checkOutDate : null,
                    CreatedAt = checkInDate.AddDays(-random.Next(1, 14))
                });
            }

            // Active reservations — checked in right now
            for (int i = 0; i < 6; i++)
            {
                var room = rooms[i * 2];
                var guest = guests[i];
                var checkInDate = DateTime.UtcNow.AddDays(-random.Next(1, 3));
                var checkOutDate = DateTime.UtcNow.AddDays(random.Next(1, 4));
                var totalAmount = Math.Round(room.PricePerNight * (checkOutDate - checkInDate).Days, 2);

                reservations.Add(new Models.Entities.Reservation
                {
                    HotelId = hotel.Id,
                    RoomId = room.Id,
                    GuestId = guest.Id,
                    CreatedByUserId = adminUser.Id,
                    BookingType = BookingType.Daily,
                    CheckInDate = checkInDate,
                    CheckOutDate = checkOutDate,
                    NumberOfGuests = random.Next(1, room.Capacity + 1),
                    Status = ReservationStatus.CheckedIn,
                    PaymentStatus = PaymentStatus.PartiallyPaid,
                    PaymentMethod = PaymentMethod.CreditCard,
                    TotalAmount = totalAmount,
                    DepositAmount = Math.Round(totalAmount * 0.3m, 2),
                    RemainingAmount = Math.Round(totalAmount * 0.7m, 2),
                    CheckedInAt = checkInDate,
                    CreatedAt = checkInDate.AddDays(-1)
                });
            }

            // Upcoming confirmed reservations — next 2 weeks
            for (int i = 0; i < 10; i++)
            {
                var room = rooms[random.Next(rooms.Count)];
                var guest = guests[random.Next(guests.Count)];
                var checkInDate = DateTime.UtcNow.AddDays(random.Next(1, 14));
                var nights = random.Next(1, 6);
                var checkOutDate = checkInDate.AddDays(nights);
                var totalAmount = Math.Round(room.PricePerNight * nights, 2);

                reservations.Add(new Models.Entities.Reservation
                {
                    HotelId = hotel.Id,
                    RoomId = room.Id,
                    GuestId = guest.Id,
                    CreatedByUserId = adminUser.Id,
                    BookingType = BookingType.Daily,
                    CheckInDate = checkInDate,
                    CheckOutDate = checkOutDate,
                    NumberOfGuests = random.Next(1, room.Capacity + 1),
                    Status = ReservationStatus.Confirmed,
                    PaymentStatus = PaymentStatus.Unpaid,
                    TotalAmount = totalAmount,
                    DepositAmount = 0,
                    RemainingAmount = totalAmount,
                    ConfirmedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 5))
                });
            }

            // Today's check-ins (pending)
            for (int i = 0; i < 3; i++)
            {
                var room = rooms[10 + i];
                var guest = guests[20 + i];
                var checkInDate = DateTime.UtcNow.Date;
                var nights = random.Next(1, 4);
                var checkOutDate = checkInDate.AddDays(nights);
                var totalAmount = Math.Round(room.PricePerNight * nights, 2);

                reservations.Add(new Models.Entities.Reservation
                {
                    HotelId = hotel.Id,
                    RoomId = room.Id,
                    GuestId = guest.Id,
                    CreatedByUserId = adminUser.Id,
                    BookingType = BookingType.Daily,
                    CheckInDate = checkInDate,
                    CheckOutDate = checkOutDate,
                    NumberOfGuests = 1,
                    Status = ReservationStatus.Confirmed,
                    PaymentStatus = PaymentStatus.Unpaid,
                    TotalAmount = totalAmount,
                    DepositAmount = 0,
                    RemainingAmount = totalAmount,
                    ConfirmedAt = DateTime.UtcNow.AddDays(-1),
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 7))
                });
            }

            context.Reservations.AddRange(reservations);
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ Created {reservations.Count} reservations (150 historical + 6 active + 10 upcoming + 3 today)");
        }

        Console.WriteLine("🎉 Mock data seeding completed!");
        Console.WriteLine($"   👤 Admin:   admin@hotel.com / Admin123!");
        Console.WriteLine($"   👤 Manager: manager@hotel.com / Manager123!");
        Console.WriteLine($"   👤 SuperAdmin: superadmin@hotel.com / SuperAdmin123!");
    }
}
