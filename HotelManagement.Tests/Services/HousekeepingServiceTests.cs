using HotelManagement.Data;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelManagement.Tests.Services;

public class HousekeepingServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly HousekeepingService _service;
    private readonly DateTime _today = DateTime.UtcNow.Date;

    public HousekeepingServiceTests()
    {
        _context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        _service = new HousekeepingService(_context);

        _context.Users.Add(new ApplicationUser { Id = "staff", UserName = "staff", FirstName = "S", LastName = "U" });
        _context.Hotels.Add(new Hotel { Id = 1, Name = "H", OwnerId = "staff", Address = "A", City = "C", Country = "X" });
        _context.Guests.Add(new Guest { Id = 1, FirstName = "G", LastName = "One", Email = "g@test.com", PhoneNumber = "1" });
        for (var i = 1; i <= 4; i++)
            _context.Rooms.Add(new Room { Id = i, HotelId = 1, RoomNumber = $"10{i}", Capacity = 2, PricePerNight = 100 });
        _context.SaveChanges();
    }

    private void AddStay(int roomId, DateTime checkIn, DateTime checkOut, ReservationStatus status, DateTime? checkedOutAt = null)
    {
        _context.Reservations.Add(new Reservation
        {
            HotelId = 1, RoomId = roomId, GuestId = 1, CreatedByUserId = "staff",
            CheckInDate = checkIn, CheckOutDate = checkOut, Status = status, CheckedOutAt = checkedOutAt
        });
    }

    [Fact]
    public async Task GenerateDailyTasks_CoversScheduledAndEarlyDepartures_ButNotNoShows()
    {
        AddStay(1, _today.AddDays(-2), _today, ReservationStatus.CheckedIn);                             // leaving today
        AddStay(2, _today.AddDays(-1), _today.AddDays(2), ReservationStatus.CheckedOut, _today.AddHours(9)); // left early today
        AddStay(3, _today.AddDays(-1), _today, ReservationStatus.Confirmed);                             // never arrived
        AddStay(4, _today, _today.AddDays(3), ReservationStatus.CheckedIn);                              // staying on
        await _context.SaveChangesAsync();

        await _service.GenerateDailyTasksAsync(1, _today, "staff");

        var taskRooms = await _context.HousekeepingTasks.Select(t => t.RoomId).OrderBy(id => id).ToListAsync();
        Assert.Equal(new[] { 1, 2 }, taskRooms);
    }

    [Fact]
    public async Task GenerateDailyTasks_DoesNotDuplicateExistingTasks()
    {
        AddStay(1, _today.AddDays(-1), _today, ReservationStatus.CheckedIn);
        await _context.SaveChangesAsync();

        await _service.GenerateDailyTasksAsync(1, _today, "staff");
        await _service.GenerateDailyTasksAsync(1, _today, "staff");

        Assert.Equal(1, await _context.HousekeepingTasks.CountAsync());
    }
}
