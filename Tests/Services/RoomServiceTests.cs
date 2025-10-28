using AutoMapper;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Repositories.Interfaces;
using HotelManagement.Services.Implementations;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Services;

public class RoomServiceTests
{
    private readonly Mock<IGenericRepository<Room>> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly RoomService _service;

    public RoomServiceTests()
    {
        _mockRepository = new Mock<IGenericRepository<Room>>();
        _mockMapper = new Mock<IMapper>();
        _service = new RoomService(_mockRepository.Object, _mockMapper.Object);
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithUniqueRoomNumber_ShouldCreateRoom()
    {
        // Arrange
        var dto = new RoomDto
        {
            HotelId = 1,
            RoomNumber = "101",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        };

        var room = new Room
        {
            HotelId = 1,
            RoomNumber = "101",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>()))
            .ReturnsAsync(new List<Room>()); // No existing rooms
        _mockMapper.Setup(m => m.Map<Room>(dto)).Returns(room);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Room>())).Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<RoomDto>(It.IsAny<Room>())).Returns(dto);

        // Act
        var result = await _service.CreateAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.RoomNumber.Should().Be("101");
        _mockRepository.Verify(r => r.AddAsync(It.Is<Room>(rm => rm.CreatedAt != default)), Times.Once);
        _mockRepository.Verify(r => r.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateRoomNumber_ShouldThrowException()
    {
        // Arrange
        var dto = new RoomDto
        {
            HotelId = 1,
            RoomNumber = "101",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        };

        var existingRooms = new List<Room>
        {
            new() { Id = 1, HotelId = 1, RoomNumber = "101" }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>()))
            .ReturnsAsync(existingRooms);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _service.CreateAsync(dto);
        });

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Room>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldSetCreatedAtTimestamp()
    {
        // Arrange
        var dto = new RoomDto
        {
            HotelId = 1,
            RoomNumber = "101",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        };

        var room = new Room { HotelId = 1, RoomNumber = "101" };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>()))
            .ReturnsAsync(new List<Room>());
        _mockMapper.Setup(m => m.Map<Room>(dto)).Returns(room);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Room>())).Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<RoomDto>(It.IsAny<Room>())).Returns(dto);

        // Act
        await _service.CreateAsync(dto);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(It.Is<Room>(rm =>
            rm.CreatedAt > DateTime.UtcNow.AddSeconds(-5) &&
            rm.CreatedAt <= DateTime.UtcNow
        )), Times.Once);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldUpdateRoom()
    {
        // Arrange
        var existingRoom = new Room
        {
            Id = 1,
            HotelId = 1,
            RoomNumber = "101",
            PricePerNight = 100
        };

        var dto = new RoomDto
        {
            Id = 1,
            HotelId = 1,
            RoomNumber = "101",
            PricePerNight = 150
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingRoom);
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>()))
            .ReturnsAsync(new List<Room>());
        _mockMapper.Setup(m => m.Map(It.IsAny<RoomDto>(), It.IsAny<Room>())).Returns(existingRoom);
        _mockRepository.Setup(r => r.Update(It.IsAny<Room>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<RoomDto>(It.IsAny<Room>())).Returns(dto);

        // Act
        var result = await _service.UpdateAsync(1, dto);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(r => r.Update(It.IsAny<Room>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldNotAllowChangingHotelId()
    {
        // Arrange
        var existingRoom = new Room
        {
            Id = 1,
            HotelId = 1,
            RoomNumber = "101"
        };

        var dto = new RoomDto
        {
            Id = 1,
            HotelId = 2, // Attempting to change hotel
            RoomNumber = "101"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingRoom);
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>()))
            .ReturnsAsync(new List<Room>());
        _mockMapper.Setup(m => m.Map(It.IsAny<RoomDto>(), It.IsAny<Room>())).Returns(existingRoom);
        _mockRepository.Setup(r => r.Update(It.IsAny<Room>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<RoomDto>(It.IsAny<Room>())).Returns(dto);

        // Act
        await _service.UpdateAsync(1, dto);

        // Assert - HotelId should remain unchanged
        dto.HotelId.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_WithNewRoomNumber_ShouldValidateUniqueness()
    {
        // Arrange
        var existingRoom = new Room
        {
            Id = 1,
            HotelId = 1,
            RoomNumber = "101"
        };

        var dto = new RoomDto
        {
            Id = 1,
            HotelId = 1,
            RoomNumber = "102" // Changing room number
        };

        var conflictingRoom = new Room { Id = 2, HotelId = 1, RoomNumber = "102" };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingRoom);
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>()))
            .ReturnsAsync(new List<Room> { conflictingRoom });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _service.UpdateAsync(1, dto);
        });
    }

    #endregion

    #region GetRoomsByHotelIdAsync Tests

    [Fact]
    public async Task GetRoomsByHotelIdAsync_ShouldReturnRoomsForHotel()
    {
        // Arrange
        var hotelId = 1;
        var rooms = new List<Room>
        {
            new() { Id = 1, HotelId = hotelId, RoomNumber = "101" },
            new() { Id = 2, HotelId = hotelId, RoomNumber = "102" }
        };

        var dtos = new List<RoomDto>
        {
            new() { Id = 1, HotelId = hotelId, RoomNumber = "101" },
            new() { Id = 2, HotelId = hotelId, RoomNumber = "102" }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>()))
            .ReturnsAsync(rooms);
        _mockMapper.Setup(m => m.Map<IEnumerable<RoomDto>>(rooms)).Returns(dtos);

        // Act
        var result = await _service.GetRoomsByHotelIdAsync(hotelId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.HotelId.Should().Be(hotelId));
    }

    #endregion

    #region GetAvailableRoomsByHotelAsync Tests

    [Fact]
    public async Task GetAvailableRoomsByHotelAsync_ShouldReturnOnlyActiveAndAvailableRooms()
    {
        // Arrange
        var hotelId = 1;
        var rooms = new List<Room>
        {
            new() { Id = 1, HotelId = hotelId, RoomNumber = "101", IsActive = true, Status = RoomStatus.Available },
            new() { Id = 2, HotelId = hotelId, RoomNumber = "102", IsActive = true, Status = RoomStatus.Available }
        };

        var dtos = new List<RoomDto>
        {
            new() { Id = 1, HotelId = hotelId, RoomNumber = "101", IsActive = true, Status = RoomStatus.Available },
            new() { Id = 2, HotelId = hotelId, RoomNumber = "102", IsActive = true, Status = RoomStatus.Available }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>()))
            .ReturnsAsync(rooms);
        _mockMapper.Setup(m => m.Map<IEnumerable<RoomDto>>(rooms)).Returns(dtos);

        // Act
        var result = await _service.GetAvailableRoomsByHotelAsync(hotelId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r =>
        {
            r.IsActive.Should().BeTrue();
            r.Status.Should().Be(RoomStatus.Available);
        });
    }

    #endregion

    #region UpdateRoomStatusAsync Tests

    [Fact]
    public async Task UpdateRoomStatusAsync_ShouldChangeStatus()
    {
        // Arrange
        var room = new Room
        {
            Id = 1,
            HotelId = 1,
            RoomNumber = "101",
            Status = RoomStatus.Available
        };

        var dto = new RoomDto
        {
            Id = 1,
            Status = RoomStatus.Cleaning
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(room);
        _mockRepository.Setup(r => r.Update(It.IsAny<Room>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<RoomDto>(It.IsAny<Room>())).Returns(dto);

        // Act
        var result = await _service.UpdateRoomStatusAsync(1, RoomStatus.Cleaning);

        // Assert
        result.Status.Should().Be(RoomStatus.Cleaning);
        _mockRepository.Verify(r => r.Update(It.Is<Room>(rm => rm.Status == RoomStatus.Cleaning)), Times.Once);
    }

    #endregion

    #region MarkRoomAsCleanedAsync Tests

    [Fact]
    public async Task MarkRoomAsCleanedAsync_ShouldSetLastCleanedAndStatusAvailable()
    {
        // Arrange
        var room = new Room
        {
            Id = 1,
            HotelId = 1,
            RoomNumber = "101",
            Status = RoomStatus.Cleaning
        };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(room);
        _mockRepository.Setup(r => r.Update(It.IsAny<Room>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _service.MarkRoomAsCleanedAsync(1);

        // Assert
        _mockRepository.Verify(r => r.Update(It.Is<Room>(rm =>
            rm.LastCleaned.HasValue &&
            rm.Status == RoomStatus.Available &&
            rm.LastCleaned.Value > DateTime.UtcNow.AddSeconds(-5)
        )), Times.Once);
    }

    #endregion

    #region RecordMaintenanceAsync Tests

    [Fact]
    public async Task RecordMaintenanceAsync_ShouldSetMaintenanceDetailsAndStatus()
    {
        // Arrange
        var room = new Room
        {
            Id = 1,
            HotelId = 1,
            RoomNumber = "101",
            Status = RoomStatus.Available
        };

        var notes = "AC unit repair";

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(room);
        _mockRepository.Setup(r => r.Update(It.IsAny<Room>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _service.RecordMaintenanceAsync(1, notes);

        // Assert
        _mockRepository.Verify(r => r.Update(It.Is<Room>(rm =>
            rm.LastMaintenance.HasValue &&
            rm.Status == RoomStatus.Maintenance &&
            rm.Notes == notes
        )), Times.Once);
    }

    #endregion

    #region IsRoomNumberUniqueAsync Tests

    [Fact]
    public async Task IsRoomNumberUniqueAsync_WithNoExistingRoom_ShouldReturnTrue()
    {
        // Arrange
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>()))
            .ReturnsAsync(new List<Room>());

        // Act
        var result = await _service.IsRoomNumberUniqueAsync(1, "101");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRoomNumberUniqueAsync_WithExistingRoom_ShouldReturnFalse()
    {
        // Arrange
        var existingRooms = new List<Room>
        {
            new() { Id = 1, HotelId = 1, RoomNumber = "101" }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>()))
            .ReturnsAsync(existingRooms);

        // Act
        var result = await _service.IsRoomNumberUniqueAsync(1, "101");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsRoomNumberUniqueAsync_ExcludingCurrentRoom_ShouldReturnTrue()
    {
        // Arrange
        var existingRooms = new List<Room>
        {
            new() { Id = 1, HotelId = 1, RoomNumber = "101" }
        };

        _mockRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Room, bool>>>()))
            .ReturnsAsync(existingRooms);

        // Act - Excluding the same room ID
        var result = await _service.IsRoomNumberUniqueAsync(1, "101", excludeRoomId: 1);

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}
