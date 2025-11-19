using AutoMapper;
using FluentAssertions;
using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Repositories.Interfaces;
using HotelManagement.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Services;

public class HotelServiceTests
{
    private readonly Mock<IGenericRepository<Hotel>> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<ApplicationDbContext> _mockContext;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly HotelService _service;

    public HotelServiceTests()
    {
        _mockRepository = new Mock<IGenericRepository<Hotel>>();
        _mockMapper = new Mock<IMapper>();
        _mockContext = new Mock<ApplicationDbContext>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _service = new HotelService(_mockRepository.Object, _mockMapper.Object, _mockContext.Object, _mockHttpContextAccessor.Object);
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidOwnerId_ShouldCreateHotel()
    {
        // Arrange
        var dto = new HotelDto
        {
            OwnerId = "user-123",
            Name = "Test Hotel",
            Address = "123 Main St",
            City = "New York",
            Country = "USA"
        };

        var hotel = new Hotel
        {
            OwnerId = "user-123",
            Name = "Test Hotel",
            Address = "123 Main St",
            City = "New York",
            Country = "USA"
        };

        _mockMapper.Setup(m => m.Map<Hotel>(dto)).Returns(hotel);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Hotel>())).Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<HotelDto>(It.IsAny<Hotel>())).Returns(dto);

        // Act
        var result = await _service.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.OwnerId.Should().Be("user-123");
        _mockRepository.Verify(r => r.AddAsync(It.Is<Hotel>(h => h.CreatedAt != default)), Times.Once);
        _mockRepository.Verify(r => r.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithoutOwnerId_ShouldThrowException()
    {
        // Arrange
        var dto = new HotelDto
        {
            OwnerId = null,
            Name = "Test Hotel",
            Address = "123 Main St",
            City = "New York",
            Country = "USA"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _service.CreateAsync(dto);
        });

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Hotel>()), Times.Never);
        _mockRepository.Verify(r => r.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldSetCreatedAtTimestamp()
    {
        // Arrange
        var dto = new HotelDto
        {
            OwnerId = "user-123",
            Name = "Test Hotel",
            Address = "123 Main St",
            City = "New York",
            Country = "USA"
        };

        var hotel = new Hotel
        {
            OwnerId = "user-123",
            Name = "Test Hotel"
        };

        _mockMapper.Setup(m => m.Map<Hotel>(dto)).Returns(hotel);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Hotel>())).Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<HotelDto>(It.IsAny<Hotel>())).Returns(dto);

        // Act
        await _service.CreateAsync(dto);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(It.Is<Hotel>(h =>
            h.CreatedAt > DateTime.UtcNow.AddSeconds(-5) &&
            h.CreatedAt <= DateTime.UtcNow
        )), Times.Once);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldNotChangeOwnerId()
    {
        // Arrange
        var existingHotel = new Hotel
        {
            Id = 1,
            OwnerId = "original-owner",
            Name = "Old Name"
        };

        var dto = new HotelDto
        {
            Id = 1,
            OwnerId = "different-owner", // Attempting to change owner
            Name = "New Name"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingHotel);
        _mockMapper.Setup(m => m.Map(It.IsAny<HotelDto>(), It.IsAny<Hotel>())).Returns(existingHotel);
        _mockRepository.Setup(r => r.Update(It.IsAny<Hotel>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<HotelDto>(It.IsAny<Hotel>())).Returns(dto);

        // Act
        await _service.UpdateAsync(1, dto);

        // Assert - OwnerId should remain unchanged
        dto.OwnerId.Should().Be("original-owner");
        _mockRepository.Verify(r => r.Update(It.IsAny<Hotel>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldSetUpdatedAtTimestamp()
    {
        // Arrange
        var existingHotel = new Hotel
        {
            Id = 1,
            OwnerId = "user-123",
            Name = "Old Name"
        };

        var dto = new HotelDto
        {
            Id = 1,
            OwnerId = "user-123",
            Name = "New Name"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingHotel);
        _mockMapper.Setup(m => m.Map(It.IsAny<HotelDto>(), It.IsAny<Hotel>())).Returns(existingHotel);
        _mockRepository.Setup(r => r.Update(It.IsAny<Hotel>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<HotelDto>(It.IsAny<Hotel>())).Returns(dto);

        // Act
        await _service.UpdateAsync(1, dto);

        // Assert
        _mockRepository.Verify(r => r.Update(It.Is<Hotel>(h =>
            h.UpdatedAt.HasValue &&
            h.UpdatedAt.Value > DateTime.UtcNow.AddSeconds(-5) &&
            h.UpdatedAt.Value <= DateTime.UtcNow
        )), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentHotel_ShouldThrowException()
    {
        // Arrange
        var dto = new HotelDto { Id = 999, Name = "Test" };
        _mockRepository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Hotel?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            await _service.UpdateAsync(999, dto);
        });
    }

    #endregion

    #region GetHotelsByOwnerAsync Tests

    [Fact]
    public async Task GetHotelsByOwnerAsync_ShouldReturnOnlyOwnersHotels()
    {
        // Arrange
        var ownerId = "user-123";
        var hotels = new List<Hotel>
        {
            new() { Id = 1, OwnerId = ownerId, Name = "Hotel 1" },
            new() { Id = 2, OwnerId = ownerId, Name = "Hotel 2" }
        };

        var dtos = new List<HotelDto>
        {
            new() { Id = 1, OwnerId = ownerId, Name = "Hotel 1" },
            new() { Id = 2, OwnerId = ownerId, Name = "Hotel 2" }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Hotel, bool>>>()))
            .ReturnsAsync(hotels);
        _mockMapper.Setup(m => m.Map<IEnumerable<HotelDto>>(hotels)).Returns(dtos);

        // Act
        var result = await _service.GetHotelsByOwnerAsync(ownerId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(h => h.OwnerId.Should().Be(ownerId));
    }

    [Fact]
    public async Task GetHotelsByOwnerAsync_WithNoHotels_ShouldReturnEmpty()
    {
        // Arrange
        var ownerId = "user-with-no-hotels";
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Hotel, bool>>>()))
            .ReturnsAsync(new List<Hotel>());
        _mockMapper.Setup(m => m.Map<IEnumerable<HotelDto>>(It.IsAny<IEnumerable<Hotel>>()))
            .Returns(new List<HotelDto>());

        // Act
        var result = await _service.GetHotelsByOwnerAsync(ownerId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetAllHotelsForUserAsync Tests

    [Fact]
    public async Task GetAllHotelsForUserAsync_AsSuperAdmin_ShouldReturnAllHotels()
    {
        // Arrange
        var superAdminId = "super-admin-id";
        var allHotels = new List<Hotel>
        {
            new() { Id = 1, OwnerId = "user-1", Name = "Hotel 1" },
            new() { Id = 2, OwnerId = "user-2", Name = "Hotel 2" },
            new() { Id = 3, OwnerId = "user-3", Name = "Hotel 3" }
        };

        var allDtos = new List<HotelDto>
        {
            new() { Id = 1, OwnerId = "user-1", Name = "Hotel 1" },
            new() { Id = 2, OwnerId = "user-2", Name = "Hotel 2" },
            new() { Id = 3, OwnerId = "user-3", Name = "Hotel 3" }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(allHotels);
        _mockMapper.Setup(m => m.Map<IEnumerable<HotelDto>>(allHotels)).Returns(allDtos);

        // Act
        var result = await _service.GetAllHotelsForUserAsync(superAdminId, isSuperAdmin: true);

        // Assert
        result.Should().HaveCount(3);
        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
        _mockRepository.Verify(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Hotel, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task GetAllHotelsForUserAsync_AsRegularAdmin_ShouldReturnOnlyOwnHotels()
    {
        // Arrange
        var adminId = "admin-123";
        var ownHotels = new List<Hotel>
        {
            new() { Id = 1, OwnerId = adminId, Name = "My Hotel 1" },
            new() { Id = 2, OwnerId = adminId, Name = "My Hotel 2" }
        };

        var ownDtos = new List<HotelDto>
        {
            new() { Id = 1, OwnerId = adminId, Name = "My Hotel 1" },
            new() { Id = 2, OwnerId = adminId, Name = "My Hotel 2" }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Hotel, bool>>>()))
            .ReturnsAsync(ownHotels);
        _mockMapper.Setup(m => m.Map<IEnumerable<HotelDto>>(ownHotels)).Returns(ownDtos);

        // Act
        var result = await _service.GetAllHotelsForUserAsync(adminId, isSuperAdmin: false);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(h => h.OwnerId.Should().Be(adminId));
        _mockRepository.Verify(r => r.GetAllAsync(), Times.Never);
        _mockRepository.Verify(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Hotel, bool>>>()), Times.Once);
    }

    #endregion
}
