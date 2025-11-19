using AutoMapper;
using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using Xunit;

namespace HotelManagement.Tests.Services;

/// <summary>
/// Tests for improved availability checking logic with buffer times and booking type conflicts
/// </summary>
public class AvailabilityTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly ReservationService _service;

    public AvailabilityTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mapperMock = new Mock<IMapper>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id")
        }));
        var httpContext = new DefaultHttpContext { User = user };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        _service = new ReservationService(_context, _mapperMock.Object, _httpContextAccessorMock.Object);
    }

    private async Task SeedTestData()
    {
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "owner-id",
            Address = "123 Test St",
            City = "Test City",
            Country = "Test Country"
        };

        var room = new Room
        {
            Id = 1,
            HotelId = 1,
            RoomNumber = "101",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 150,
            Status = RoomStatus.Available,
            AllowsShortStay = true,
            ShortStayHourlyRate = 25,
            MinimumShortStayHours = 2,
            MaximumShortStayHours = 12
        };

        var guest = new Guest
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            PhoneNumber = "+1-555-0100"
        };

        var user = new ApplicationUser
        {
            Id = "test-user-id",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            UserName = "test@example.com"
        };

        _context.Hotels.Add(hotel);
        _context.Rooms.Add(room);
        _context.Guests.Add(guest);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task SameDayTurnover_WithinBufferTime_ShouldFail()
    {
        // Arrange
        await SeedTestData();
        var baseDate = DateTime.UtcNow.AddDays(5).Date;

        // Existing reservation: 8 AM - 11 AM
        var existingReservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = baseDate.AddHours(8),
            CheckOutDate = baseDate.AddHours(11),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 150,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(existingReservation);
        await _context.SaveChangesAsync();

        // Try to book at 12 PM (only 1 hour after check-out, needs 3 hours buffer)
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.Daily,
            CheckInDate = baseDate.AddHours(12),
            CheckOutDate = baseDate.AddHours(15),
            NumberOfGuests = 2
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateReservationAsync(createDto));
        Assert.Contains("Earliest check-in", exception.Message);
        Assert.Contains("3h cleaning buffer", exception.Message);
    }

    [Fact]
    public async Task SameDayTurnover_AfterBufferTime_ShouldSucceed()
    {
        // Arrange
        await SeedTestData();
        var baseDate = DateTime.UtcNow.AddDays(5).Date;

        // Existing reservation: 8 AM - 11 AM
        var existingReservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = baseDate.AddHours(8),
            CheckOutDate = baseDate.AddHours(11),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 150,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(existingReservation);
        await _context.SaveChangesAsync();

        // Book at 2 PM (3 hours after check-out = exactly at buffer boundary)
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.Daily,
            CheckInDate = baseDate.AddHours(14),
            CheckOutDate = baseDate.AddDays(1).AddHours(11),
            NumberOfGuests = 2
        };

        // Act
        var result = await _service.CreateReservationAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(baseDate.AddHours(14), result.CheckInDate);
    }

    [Fact]
    public async Task ShortStay_OverlapsWithOvernightBooking_ShouldFail()
    {
        // Arrange
        await SeedTestData();
        var baseDate = DateTime.UtcNow.AddDays(5).Date;

        // Existing overnight reservation
        var existingReservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = baseDate,
            CheckOutDate = baseDate.AddDays(2),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 300,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(existingReservation);
        await _context.SaveChangesAsync();

        // Try to book short-stay on same day
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.ShortStay,
            CheckInDate = baseDate.AddHours(14),
            CheckOutDate = baseDate.AddHours(18),
            NumberOfGuests = 2
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateReservationAsync(createDto));
        Assert.Contains("booked overnight", exception.Message);
    }

    [Fact]
    public async Task OvernightBooking_WithExistingShortStay_ShouldFail()
    {
        // Arrange
        await SeedTestData();
        var baseDate = DateTime.UtcNow.AddDays(5).Date;

        // Existing short-stay reservation
        var existingReservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.ShortStay,
            CheckInDate = baseDate.AddHours(14),
            CheckOutDate = baseDate.AddHours(18),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 100,
            DurationInHours = 4,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(existingReservation);
        await _context.SaveChangesAsync();

        // Try to book overnight that includes the short-stay day
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.Daily,
            CheckInDate = baseDate,
            CheckOutDate = baseDate.AddDays(2),
            NumberOfGuests = 2
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateReservationAsync(createDto));
        Assert.Contains("short-stay booking", exception.Message);
    }

    [Fact]
    public async Task TwoShortStays_SameDay_NoOverlap_ShouldSucceed()
    {
        // Arrange
        await SeedTestData();
        var baseDate = DateTime.UtcNow.AddDays(5).Date;

        // Existing short-stay: 10 AM - 12 PM
        var existingReservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.ShortStay,
            CheckInDate = baseDate.AddHours(10),
            CheckOutDate = baseDate.AddHours(12),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 50,
            DurationInHours = 2,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(existingReservation);
        await _context.SaveChangesAsync();

        // New short-stay: 2 PM - 6 PM (no overlap)
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.ShortStay,
            CheckInDate = baseDate.AddHours(14),
            CheckOutDate = baseDate.AddHours(18),
            NumberOfGuests = 2
        };

        // Act
        var result = await _service.CreateReservationAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingType.ShortStay, result.BookingType);
    }

    [Fact]
    public async Task TwoShortStays_SameDay_WithOverlap_ShouldFail()
    {
        // Arrange
        await SeedTestData();
        var baseDate = DateTime.UtcNow.AddDays(5).Date;

        // Existing short-stay: 2 PM - 6 PM
        var existingReservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.ShortStay,
            CheckInDate = baseDate.AddHours(14),
            CheckOutDate = baseDate.AddHours(18),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 100,
            DurationInHours = 4,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(existingReservation);
        await _context.SaveChangesAsync();

        // Try to book: 4 PM - 8 PM (overlaps 4-6 PM)
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.ShortStay,
            CheckInDate = baseDate.AddHours(16),
            CheckOutDate = baseDate.AddHours(20),
            NumberOfGuests = 2
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateReservationAsync(createDto));
        Assert.Contains("Overlaps with short-stay booking", exception.Message);
    }

    [Fact]
    public async Task GetAvailableRooms_FiltersCorrectly()
    {
        // Arrange
        await SeedTestData();
        
        // Add more rooms
        _context.Rooms.Add(new Room
        {
            Id = 2,
            HotelId = 1,
            RoomNumber = "102",
            Type = RoomType.Suite,
            Capacity = 4,
            PricePerNight = 250,
            Status = RoomStatus.Available,
            AllowsShortStay = true,
            ShortStayHourlyRate = 40
        });
        
        _context.Rooms.Add(new Room
        {
            Id = 3,
            HotelId = 1,
            RoomNumber = "103",
            Type = RoomType.Single,
            Capacity = 1,
            PricePerNight = 100,
            Status = RoomStatus.Available,
            AllowsShortStay = false
        });
        await _context.SaveChangesAsync();

        var checkIn = DateTime.UtcNow.AddDays(10);
        var checkOut = DateTime.UtcNow.AddDays(12);

        // Act
        var result = await _service.GetAvailableRoomsAsync(
            hotelId: 1,
            checkIn: checkIn,
            checkOut: checkOut,
            bookingType: BookingType.ShortStay,
            minCapacity: 2);

        // Assert
        Assert.Equal(2, result.Count()); // Room 101 and 102 (both allow short-stay and capacity >= 2)
        Assert.DoesNotContain(result, r => r.RoomNumber == "103"); // Single room excluded (capacity < 2)
    }

    [Fact]
    public async Task GetAvailableRooms_ExcludesBookedRooms()
    {
        // Arrange
        await SeedTestData();
        var checkIn = DateTime.UtcNow.AddDays(10);
        var checkOut = DateTime.UtcNow.AddDays(12);

        // Book room 101
        var existingReservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 300,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(existingReservation);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableRoomsAsync(
            hotelId: 1,
            checkIn: checkIn,
            checkOut: checkOut,
            bookingType: BookingType.Daily);

        // Assert
        Assert.Empty(result); // Room 101 is booked, no other rooms in test data
    }

    [Fact]
    public async Task CheckedOutReservation_DoesNotBlockAvailability()
    {
        // Arrange
        await SeedTestData();
        var checkIn = DateTime.UtcNow.AddDays(10);
        var checkOut = DateTime.UtcNow.AddDays(12);

        // Create checked-out reservation (shouldn't block)
        var checkedOutReservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            Status = ReservationStatus.CheckedOut, // Checked out!
            NumberOfGuests = 2,
            TotalAmount = 300,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(checkedOutReservation);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.IsRoomAvailableAsync(1, checkIn, checkOut);

        // Assert
        Assert.True(result); // Checked-out reservations don't block
    }

    [Fact]
    public async Task DetailedErrorMessage_IncludesConflictInfo()
    {
        // Arrange
        await SeedTestData();
        var baseDate = DateTime.UtcNow.AddDays(5).Date;

        // Existing reservation
        var existingReservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = baseDate,
            CheckOutDate = baseDate.AddDays(2),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 300,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(existingReservation);
        await _context.SaveChangesAsync();

        // Try to book overlapping dates
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.Daily,
            CheckInDate = baseDate.AddDays(1),
            CheckOutDate = baseDate.AddDays(3),
            NumberOfGuests = 2
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateReservationAsync(createDto));
        
        // Verify error message contains useful info
        Assert.Contains("not available", exception.Message);
        Assert.Contains("Guest:", exception.Message); // Should include guest name
        Assert.Contains("Reservation #", exception.Message); // Should include reservation ID
    }
}
