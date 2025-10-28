using AutoMapper;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Repositories.Interfaces;
using HotelManagement.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Services;

public class GuestServiceTests
{
    private readonly Mock<IGenericRepository<Guest>> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly GuestService _service;

    public GuestServiceTests()
    {
        _mockRepository = new Mock<IGenericRepository<Guest>>();
        _mockMapper = new Mock<IMapper>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _service = new GuestService(_mockRepository.Object, _mockMapper.Object, _mockHttpContextAccessor.Object);
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithUniqueEmail_ShouldCreateGuest()
    {
        // Arrange
        var dto = new GuestDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            PhoneNumber = "+1-555-0100"
        };

        var guest = new Guest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            PhoneNumber = "+1-555-0100"
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(new List<Guest>()); // No existing guests with this email
        _mockMapper.Setup(m => m.Map<Guest>(dto)).Returns(guest);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Guest>())).Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<GuestDto>(It.IsAny<Guest>())).Returns(dto);

        // Act
        var result = await _service.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("john.doe@example.com");
        _mockRepository.Verify(r => r.AddAsync(It.Is<Guest>(g => g.CreatedAt != default)), Times.Once);
        _mockRepository.Verify(r => r.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateEmail_ShouldThrowException()
    {
        // Arrange
        var dto = new GuestDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "existing@example.com",
            PhoneNumber = "+1-555-0100"
        };

        var existingGuests = new List<Guest>
        {
            new() { Id = 1, Email = "existing@example.com" }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(existingGuests);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _service.CreateAsync(dto);
        });

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Guest>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldSetCreatedAtTimestamp()
    {
        // Arrange
        var dto = new GuestDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            PhoneNumber = "+1-555-0100"
        };

        var guest = new Guest { FirstName = "John", LastName = "Doe" };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(new List<Guest>());
        _mockMapper.Setup(m => m.Map<Guest>(dto)).Returns(guest);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Guest>())).Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<GuestDto>(It.IsAny<Guest>())).Returns(dto);

        // Act
        await _service.CreateAsync(dto);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(It.Is<Guest>(g =>
            g.CreatedAt > DateTime.UtcNow.AddSeconds(-5) &&
            g.CreatedAt <= DateTime.UtcNow
        )), Times.Once);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldUpdateGuest()
    {
        // Arrange
        var existingGuest = new Guest
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            PhoneNumber = "+1-555-0100"
        };

        var dto = new GuestDto
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            PhoneNumber = "+1-555-0200"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingGuest);
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(new List<Guest>());
        _mockMapper.Setup(m => m.Map(It.IsAny<GuestDto>(), It.IsAny<Guest>())).Returns(existingGuest);
        _mockRepository.Setup(r => r.Update(It.IsAny<Guest>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<GuestDto>(It.IsAny<Guest>())).Returns(dto);

        // Act
        var result = await _service.UpdateAsync(1, dto);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(r => r.Update(It.IsAny<Guest>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNewEmail_ShouldValidateUniqueness()
    {
        // Arrange
        var existingGuest = new Guest
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "old@example.com"
        };

        var dto = new GuestDto
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "new@example.com",
            PhoneNumber = "+1-555-0100"
        };

        var conflictingGuest = new Guest { Id = 2, Email = "new@example.com" };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingGuest);
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(new List<Guest> { conflictingGuest });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _service.UpdateAsync(1, dto);
        });
    }

    #endregion

    #region SearchByNameAsync Tests

    [Fact]
    public async Task SearchByNameAsync_ShouldReturnMatchingGuests()
    {
        // Arrange
        var guests = new List<Guest>
        {
            new() { Id = 1, FirstName = "John", LastName = "Doe" },
            new() { Id = 2, FirstName = "Jane", LastName = "Smith" }
        };

        var dtos = new List<GuestDto>
        {
            new() { Id = 1, FirstName = "John", LastName = "Doe" },
            new() { Id = 2, FirstName = "Jane", LastName = "Smith" }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(guests);
        _mockMapper.Setup(m => m.Map<IEnumerable<GuestDto>>(guests)).Returns(dtos);

        // Act
        var result = await _service.SearchByNameAsync("John");

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region GetByEmailAsync Tests

    [Fact]
    public async Task GetByEmailAsync_WhenExists_ShouldReturnGuest()
    {
        // Arrange
        var guest = new Guest { Id = 1, Email = "john.doe@example.com" };
        var dto = new GuestDto { Id = 1, Email = "john.doe@example.com" };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(new List<Guest> { guest });
        _mockMapper.Setup(m => m.Map<GuestDto>(guest)).Returns(dto);

        // Act
        var result = await _service.GetByEmailAsync("john.doe@example.com");

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("john.doe@example.com");
    }

    [Fact]
    public async Task GetByEmailAsync_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(new List<Guest>());

        // Act
        var result = await _service.GetByEmailAsync("nonexistent@example.com");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region VIP Management Tests

    [Fact]
    public async Task GetVIPGuestsAsync_ShouldReturnOnlyActiveVIPGuests()
    {
        // Arrange
        var guests = new List<Guest>
        {
            new() { Id = 1, FirstName = "VIP", IsVIP = true, IsActive = true, IsBlacklisted = false },
            new() { Id = 2, FirstName = "VIP2", IsVIP = true, IsActive = true, IsBlacklisted = false }
        };

        var dtos = new List<GuestDto>
        {
            new() { Id = 1, FirstName = "VIP", IsVIP = true },
            new() { Id = 2, FirstName = "VIP2", IsVIP = true }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(guests);
        _mockMapper.Setup(m => m.Map<IEnumerable<GuestDto>>(guests)).Returns(dtos);

        // Act
        var result = await _service.GetVIPGuestsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(g => g.IsVIP.Should().BeTrue());
    }

    [Fact]
    public async Task SetVIPStatusAsync_ShouldUpdateVIPFlag()
    {
        // Arrange
        var guest = new Guest { Id = 1, IsVIP = false };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(guest);
        _mockRepository.Setup(r => r.Update(It.IsAny<Guest>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _service.SetVIPStatusAsync(1, true);

        // Assert
        _mockRepository.Verify(r => r.Update(It.Is<Guest>(g => g.IsVIP == true)), Times.Once);
    }

    #endregion

    #region Blacklist Management Tests

    [Fact]
    public async Task BlacklistGuestAsync_ShouldSetBlacklistFlagAndReason()
    {
        // Arrange
        var guest = new Guest { Id = 1, IsBlacklisted = false };
        var reason = "Payment issues";

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(guest);
        _mockRepository.Setup(r => r.Update(It.IsAny<Guest>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _service.BlacklistGuestAsync(1, reason);

        // Assert
        _mockRepository.Verify(r => r.Update(It.Is<Guest>(g =>
            g.IsBlacklisted == true &&
            g.BlacklistReason == reason
        )), Times.Once);
    }

    [Fact]
    public async Task UnblacklistGuestAsync_ShouldClearBlacklistFlagAndReason()
    {
        // Arrange
        var guest = new Guest { Id = 1, IsBlacklisted = true, BlacklistReason = "Previous issue" };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(guest);
        _mockRepository.Setup(r => r.Update(It.IsAny<Guest>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _service.UnblacklistGuestAsync(1);

        // Assert
        _mockRepository.Verify(r => r.Update(It.Is<Guest>(g =>
            g.IsBlacklisted == false &&
            g.BlacklistReason == null
        )), Times.Once);
    }

    [Fact]
    public async Task GetBlacklistedGuestsAsync_ShouldReturnOnlyBlacklistedGuests()
    {
        // Arrange
        var guests = new List<Guest>
        {
            new() { Id = 1, FirstName = "Blacklisted", IsBlacklisted = true }
        };

        var dtos = new List<GuestDto>
        {
            new() { Id = 1, FirstName = "Blacklisted", IsBlacklisted = true }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(guests);
        _mockMapper.Setup(m => m.Map<IEnumerable<GuestDto>>(guests)).Returns(dtos);

        // Act
        var result = await _service.GetBlacklistedGuestsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.Should().AllSatisfy(g => g.IsBlacklisted.Should().BeTrue());
    }

    #endregion

    #region IsEmailUniqueAsync Tests

    [Fact]
    public async Task IsEmailUniqueAsync_WithNoExistingGuest_ShouldReturnTrue()
    {
        // Arrange
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(new List<Guest>());

        // Act
        var result = await _service.IsEmailUniqueAsync("new@example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmailUniqueAsync_WithExistingGuest_ShouldReturnFalse()
    {
        // Arrange
        var existingGuests = new List<Guest>
        {
            new() { Id = 1, Email = "existing@example.com" }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(existingGuests);

        // Act
        var result = await _service.IsEmailUniqueAsync("existing@example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEmailUniqueAsync_ExcludingCurrentGuest_ShouldReturnTrue()
    {
        // Arrange
        var existingGuests = new List<Guest>
        {
            new() { Id = 1, Email = "existing@example.com" }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Guest, bool>>>()))
            .ReturnsAsync(existingGuests);

        // Act - Excluding the same guest ID
        var result = await _service.IsEmailUniqueAsync("existing@example.com", excludeGuestId: 1);

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}
