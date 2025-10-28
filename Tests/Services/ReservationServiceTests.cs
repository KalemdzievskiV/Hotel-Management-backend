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

public class ReservationServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly ReservationService _service;

    public ReservationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mapperMock = new Mock<IMapper>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        // Setup HTTP context with user ID
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
    public async Task CreateReservationAsync_DailyBooking_CalculatesPriceCorrectly()
    {
        // Arrange
        await SeedTestData();
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(1),
            CheckOutDate = DateTime.UtcNow.AddDays(3), // 2 nights
            NumberOfGuests = 2,
            DepositAmount = 100
        };

        // Act
        var result = await _service.CreateReservationAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(300, result.TotalAmount); // 2 nights × $150
        Assert.Equal(100, result.DepositAmount);
        Assert.Equal(200, result.RemainingAmount);
        Assert.Equal(PaymentStatus.PartiallyPaid, result.PaymentStatus);
        Assert.Equal(ReservationStatus.Pending, result.Status);
    }

    [Fact]
    public async Task CreateReservationAsync_ShortStayBooking_CalculatesPriceCorrectly()
    {
        // Arrange
        await SeedTestData();
        var checkIn = DateTime.UtcNow.AddDays(1);
        var checkOut = checkIn.AddHours(4); // 4 hours
        
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.ShortStay,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            NumberOfGuests = 2,
            DepositAmount = 0
        };

        // Act
        var result = await _service.CreateReservationAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.TotalAmount); // 4 hours × $25
        Assert.Equal(4, result.DurationInHours);
        Assert.Equal(PaymentStatus.Unpaid, result.PaymentStatus);
    }

    [Fact]
    public async Task CreateReservationAsync_ShortStayOnNonShortStayRoom_ThrowsException()
    {
        // Arrange
        await SeedTestData();
        var room = await _context.Rooms.FindAsync(1);
        room!.AllowsShortStay = false;
        await _context.SaveChangesAsync();

        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.ShortStay,
            CheckInDate = DateTime.UtcNow.AddDays(1),
            CheckOutDate = DateTime.UtcNow.AddDays(1).AddHours(4),
            NumberOfGuests = 2
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateReservationAsync(createDto));
        Assert.Contains("does not support short-stay", exception.Message);
    }

    [Fact]
    public async Task CreateReservationAsync_InvalidDates_ThrowsException()
    {
        // Arrange
        await SeedTestData();
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(3),
            CheckOutDate = DateTime.UtcNow.AddDays(1), // Before check-in!
            NumberOfGuests = 2
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateReservationAsync(createDto));
        Assert.Contains("Check-out date must be after check-in date", exception.Message);
    }

    [Fact]
    public async Task CreateReservationAsync_ExceedsRoomCapacity_ThrowsException()
    {
        // Arrange
        await SeedTestData();
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(1),
            CheckOutDate = DateTime.UtcNow.AddDays(2),
            NumberOfGuests = 5 // Room capacity is 2
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateReservationAsync(createDto));
        Assert.Contains("Room capacity", exception.Message);
    }

    [Fact]
    public async Task CreateReservationAsync_ShortStayBelowMinimum_ThrowsException()
    {
        // Arrange
        await SeedTestData();
        var checkIn = DateTime.UtcNow.AddDays(1);
        var checkOut = checkIn.AddHours(1); // Only 1 hour, minimum is 2
        
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.ShortStay,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            NumberOfGuests = 2
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateReservationAsync(createDto));
        Assert.Contains("Minimum stay", exception.Message);
    }

    [Fact]
    public async Task CreateReservationAsync_ShortStayExceedsMaximum_ThrowsException()
    {
        // Arrange
        await SeedTestData();
        var checkIn = DateTime.UtcNow.AddDays(1);
        var checkOut = checkIn.AddHours(15); // 15 hours, maximum is 12
        
        var createDto = new CreateReservationDto
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            BookingType = BookingType.ShortStay,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            NumberOfGuests = 2
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateReservationAsync(createDto));
        Assert.Contains("Maximum stay", exception.Message);
    }

    [Fact]
    public async Task IsRoomAvailableAsync_NoConflicts_ReturnsTrue()
    {
        // Arrange
        await SeedTestData();
        var checkIn = DateTime.UtcNow.AddDays(10);
        var checkOut = DateTime.UtcNow.AddDays(12);

        // Act
        var result = await _service.IsRoomAvailableAsync(1, checkIn, checkOut);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsRoomAvailableAsync_WithConflict_ReturnsFalse()
    {
        // Arrange
        await SeedTestData();
        
        // Create existing reservation
        var existingReservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(8),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 450,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(existingReservation);
        await _context.SaveChangesAsync();

        // Try to book overlapping dates
        var checkIn = DateTime.UtcNow.AddDays(6);
        var checkOut = DateTime.UtcNow.AddDays(9);

        // Act
        var result = await _service.IsRoomAvailableAsync(1, checkIn, checkOut);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsRoomAvailableAsync_CancelledReservationDoesNotBlock_ReturnsTrue()
    {
        // Arrange
        await SeedTestData();
        
        // Create cancelled reservation
        var cancelledReservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(8),
            Status = ReservationStatus.Cancelled, // Cancelled!
            NumberOfGuests = 2,
            TotalAmount = 450,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(cancelledReservation);
        await _context.SaveChangesAsync();

        // Try to book same dates
        var checkIn = DateTime.UtcNow.AddDays(6);
        var checkOut = DateTime.UtcNow.AddDays(9);

        // Act
        var result = await _service.IsRoomAvailableAsync(1, checkIn, checkOut);

        // Assert
        Assert.True(result); // Cancelled reservations don't block
    }

    [Fact]
    public async Task ConfirmReservationAsync_PendingReservation_ChangesStatusToConfirmed()
    {
        // Arrange
        await SeedTestData();
        var reservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            Status = ReservationStatus.Pending,
            NumberOfGuests = 2,
            TotalAmount = 300,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ConfirmReservationAsync(reservation.Id);

        // Assert
        Assert.Equal(ReservationStatus.Confirmed, result.Status);
        Assert.NotNull(result.ConfirmedAt);
    }

    [Fact]
    public async Task ConfirmReservationAsync_NonPendingReservation_ThrowsException()
    {
        // Arrange
        await SeedTestData();
        var reservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            Status = ReservationStatus.Confirmed, // Already confirmed
            NumberOfGuests = 2,
            TotalAmount = 300,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ConfirmReservationAsync(reservation.Id));
        Assert.Contains("Only pending reservations can be confirmed", exception.Message);
    }

    [Fact]
    public async Task CheckInReservationAsync_ConfirmedReservation_SetsStatusAndRoomOccupied()
    {
        // Arrange
        await SeedTestData();
        var reservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(-1), // In the past, so can check in
            CheckOutDate = DateTime.UtcNow.AddDays(2),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 300,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CheckInReservationAsync(reservation.Id);

        // Assert
        Assert.Equal(ReservationStatus.CheckedIn, result.Status);
        Assert.NotNull(result.CheckedInAt);
        
        var room = await _context.Rooms.FindAsync(1);
        Assert.Equal(RoomStatus.Occupied, room!.Status);
    }

    [Fact]
    public async Task CheckOutReservationAsync_CheckedInReservation_SetsStatusAndRoomCleaning()
    {
        // Arrange
        await SeedTestData();
        var room = await _context.Rooms.FindAsync(1);
        room!.Status = RoomStatus.Occupied;
        
        var reservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(-2),
            CheckOutDate = DateTime.UtcNow,
            Status = ReservationStatus.CheckedIn,
            NumberOfGuests = 2,
            TotalAmount = 300,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CheckOutReservationAsync(reservation.Id);

        // Assert
        Assert.Equal(ReservationStatus.CheckedOut, result.Status);
        Assert.NotNull(result.CheckedOutAt);
        
        var updatedRoom = await _context.Rooms.FindAsync(1);
        Assert.Equal(RoomStatus.Cleaning, updatedRoom!.Status);
    }

    [Fact]
    public async Task CancelReservationAsync_PendingReservation_SetsStatusToCancelled()
    {
        // Arrange
        await SeedTestData();
        var reservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            Status = ReservationStatus.Pending,
            NumberOfGuests = 2,
            TotalAmount = 300,
            DepositAmount = 100,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CancelReservationAsync(reservation.Id, "Guest requested");

        // Assert
        Assert.Equal(ReservationStatus.Cancelled, result.Status);
        Assert.Equal("Guest requested", result.CancellationReason);
        Assert.NotNull(result.CancelledAt);
        Assert.Equal(PaymentStatus.Refunding, result.PaymentStatus); // Since deposit was made
    }

    [Fact]
    public async Task RecordPaymentAsync_ValidAmount_UpdatesPaymentStatus()
    {
        // Arrange
        await SeedTestData();
        var reservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 300,
            DepositAmount = 100,
            RemainingAmount = 200,
            PaymentStatus = PaymentStatus.PartiallyPaid,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RecordPaymentAsync(reservation.Id, 200, PaymentMethod.Cash, "CASH-123");

        // Assert
        Assert.Equal(300, result.DepositAmount); // 100 + 200
        Assert.Equal(0, result.RemainingAmount);
        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
        Assert.Equal(PaymentMethod.Cash, result.PaymentMethod);
    }

    [Fact]
    public async Task RecordPaymentAsync_ExceedsRemaining_ThrowsException()
    {
        // Arrange
        await SeedTestData();
        var reservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 300,
            DepositAmount = 100,
            RemainingAmount = 200,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RecordPaymentAsync(reservation.Id, 250, PaymentMethod.Cash)); // 250 > 200 remaining
        Assert.Contains("exceeds remaining balance", exception.Message);
    }

    [Fact]
    public async Task GetTotalRevenueAsync_OnlyCountsCheckedOutReservations()
    {
        // Arrange
        await SeedTestData();
        
        // Checked out reservation - should count
        _context.Reservations.Add(new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(-5),
            CheckOutDate = DateTime.UtcNow.AddDays(-3),
            Status = ReservationStatus.CheckedOut,
            NumberOfGuests = 2,
            TotalAmount = 300,
            CreatedAt = DateTime.UtcNow
        });
        
        // Confirmed reservation - should NOT count
        _context.Reservations.Add(new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 400,
            CreatedAt = DateTime.UtcNow
        });
        
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTotalRevenueAsync();

        // Assert
        Assert.Equal(300, result); // Only checked-out reservation
    }

    [Fact]
    public async Task DeleteReservationAsync_PendingReservation_Succeeds()
    {
        // Arrange
        await SeedTestData();
        var reservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            Status = ReservationStatus.Pending,
            NumberOfGuests = 2,
            TotalAmount = 300,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteReservationAsync(reservation.Id);

        // Assert
        var deleted = await _context.Reservations.FindAsync(reservation.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteReservationAsync_ConfirmedReservation_ThrowsException()
    {
        // Arrange
        await SeedTestData();
        var reservation = new Reservation
        {
            HotelId = 1,
            RoomId = 1,
            GuestId = 1,
            CreatedByUserId = "test-user-id",
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            Status = ReservationStatus.Confirmed,
            NumberOfGuests = 2,
            TotalAmount = 300,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DeleteReservationAsync(reservation.Id));
        Assert.Contains("Only pending reservations can be deleted", exception.Message);
    }
}
