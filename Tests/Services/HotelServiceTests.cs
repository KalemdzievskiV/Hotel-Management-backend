using AutoMapper;
using FluentAssertions;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Mapping;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Repositories.Implementations;
using HotelManagement.Services.Implementations;
using HotelManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Services;

/// <summary>
/// HotelService queries the DbContext directly (with SQL projections), so these tests use a real
/// in-memory database and the real AutoMapper profile instead of mocks.
/// </summary>
public class HotelServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly HotelService _service;

    public HotelServiceTests()
    {
        _context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfile>()).CreateMapper();
        _service = new HotelService(
            new GenericRepository<Hotel>(_context),
            mapper,
            _context,
            new Mock<IHotelAccessService>().Object);
    }

    private async Task<Hotel> AddHotelAsync(string ownerId, string name = "Hotel")
    {
        // Owner is a required navigation, so queries inner-join on it; the owner must exist
        if (await _context.Users.FindAsync(ownerId) == null)
            _context.Users.Add(new ApplicationUser { Id = ownerId, UserName = ownerId, FirstName = "Owner", LastName = ownerId });

        var hotel = new Hotel { OwnerId = ownerId, Name = name, Address = "1 Main St", City = "City", Country = "Country" };
        _context.Hotels.Add(hotel);
        await _context.SaveChangesAsync();
        return hotel;
    }

    private static HotelDto NewHotelDto(string? ownerId, string name = "Test Hotel") => new()
    {
        OwnerId = ownerId,
        Name = name,
        Address = "123 Main St",
        City = "New York",
        Country = "USA"
    };

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidOwnerId_ShouldCreateHotel()
    {
        var result = await _service.CreateAsync(NewHotelDto("user-123"));

        result.OwnerId.Should().Be("user-123");
        (await _context.Hotels.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_WithoutOwnerId_ShouldThrowException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(NewHotelDto(null)));

        (await _context.Hotels.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_ShouldSetCreatedAtTimestamp()
    {
        var result = await _service.CreateAsync(NewHotelDto("user-123"));

        var saved = await _context.Hotels.SingleAsync(h => h.Id == result.Id);
        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldNotChangeOwnerId()
    {
        var hotel = await AddHotelAsync("original-owner", "Old Name");

        await _service.UpdateAsync(hotel.Id, NewHotelDto("different-owner", "New Name"));

        var saved = await _context.Hotels.SingleAsync(h => h.Id == hotel.Id);
        saved.OwnerId.Should().Be("original-owner");
        saved.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateAsync_ShouldSetUpdatedAtTimestamp()
    {
        var hotel = await AddHotelAsync("user-123", "Old Name");

        await _service.UpdateAsync(hotel.Id, NewHotelDto("user-123", "New Name"));

        var saved = await _context.Hotels.SingleAsync(h => h.Id == hotel.Id);
        saved.UpdatedAt.Should().NotBeNull();
        saved.UpdatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentHotel_ShouldThrowException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateAsync(999, NewHotelDto("user-123")));
    }

    #endregion

    #region GetHotelsByOwnerAsync Tests

    [Fact]
    public async Task GetHotelsByOwnerAsync_ShouldReturnOnlyOwnersHotels()
    {
        await AddHotelAsync("user-123", "Hotel 1");
        await AddHotelAsync("user-123", "Hotel 2");
        await AddHotelAsync("someone-else", "Hotel 3");

        var result = await _service.GetHotelsByOwnerAsync("user-123");

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(h => h.OwnerId.Should().Be("user-123"));
    }

    [Fact]
    public async Task GetHotelsByOwnerAsync_WithNoHotels_ShouldReturnEmpty()
    {
        await AddHotelAsync("someone-else");

        var result = await _service.GetHotelsByOwnerAsync("user-with-no-hotels");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHotelsByOwnerAsync_ShouldIncludeRoomCount()
    {
        var hotel = await AddHotelAsync("user-123");
        _context.Rooms.AddRange(
            new Room { HotelId = hotel.Id, RoomNumber = "101" },
            new Room { HotelId = hotel.Id, RoomNumber = "102" });
        await _context.SaveChangesAsync();

        var result = await _service.GetHotelsByOwnerAsync("user-123");

        result.Single().TotalRooms.Should().Be(2);
    }

    #endregion

    #region GetAllHotelsForUserAsync Tests

    [Fact]
    public async Task GetAllHotelsForUserAsync_AsSuperAdmin_ShouldReturnAllHotels()
    {
        await AddHotelAsync("user-1");
        await AddHotelAsync("user-2");
        await AddHotelAsync("user-3");

        var result = await _service.GetAllHotelsForUserAsync("super-admin-id", isSuperAdmin: true);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllHotelsForUserAsync_AsRegularAdmin_ShouldReturnOnlyOwnHotels()
    {
        await AddHotelAsync("admin-123", "My Hotel 1");
        await AddHotelAsync("admin-123", "My Hotel 2");
        await AddHotelAsync("other-admin");

        var result = await _service.GetAllHotelsForUserAsync("admin-123", isSuperAdmin: false);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(h => h.OwnerId.Should().Be("admin-123"));
    }

    #endregion
}
