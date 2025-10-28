using AutoMapper;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Repositories.Interfaces;
using HotelManagement.Services.Implementations;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Services;

public class CrudServiceTests
{
    private readonly Mock<IGenericRepository<Hotel>> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;
    private readonly CrudService<Hotel, HotelDto> _service;

    public CrudServiceTests()
    {
        _mockRepository = new Mock<IGenericRepository<Hotel>>();
        _mockMapper = new Mock<IMapper>();
        _service = new CrudService<Hotel, HotelDto>(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllHotels()
    {
        // Arrange
        var hotels = new List<Hotel>
        {
            new() { Id = 1, Name = "Hotel 1", OwnerId = "user1", Address = "123 St", City = "City", Country = "Country" },
            new() { Id = 2, Name = "Hotel 2", OwnerId = "user2", Address = "456 St", City = "City", Country = "Country" }
        };

        var hotelDtos = new List<HotelDto>
        {
            new() { Id = 1, Name = "Hotel 1", Address = "123 St", City = "City", Country = "Country" },
            new() { Id = 2, Name = "Hotel 2", Address = "456 St", City = "City", Country = "Country" }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(hotels);
        _mockMapper.Setup(m => m.Map<IEnumerable<HotelDto>>(hotels)).Returns(hotelDtos);

        // Act
        var result = await _service.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(hotelDtos);
        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WhenHotelExists_ShouldReturnHotel()
    {
        // Arrange
        var hotel = new Hotel { Id = 1, Name = "Test Hotel", OwnerId = "user1", Address = "123 St", City = "City", Country = "Country" };
        var hotelDto = new HotelDto { Id = 1, Name = "Test Hotel", Address = "123 St", City = "City", Country = "Country" };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(hotel);
        _mockMapper.Setup(m => m.Map<HotelDto>(hotel)).Returns(hotelDto);

        // Act
        var result = await _service.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(hotelDto);
        _mockRepository.Verify(r => r.GetByIdAsync(1), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WhenHotelDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Hotel?)null);

        // Act
        var result = await _service.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(r => r.GetByIdAsync(999), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateAndReturnHotel()
    {
        // Arrange
        var hotelDto = new HotelDto { Name = "New Hotel", Address = "123 St", City = "City", Country = "Country" };
        var hotel = new Hotel { Id = 1, Name = "New Hotel", OwnerId = "user1", Address = "123 St", City = "City", Country = "Country" };
        var createdHotelDto = new HotelDto { Id = 1, Name = "New Hotel", Address = "123 St", City = "City", Country = "Country" };

        _mockMapper.Setup(m => m.Map<Hotel>(hotelDto)).Returns(hotel);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Hotel>())).Returns(Task.CompletedTask);
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<HotelDto>(It.IsAny<Hotel>())).Returns(createdHotelDto);

        // Act
        var result = await _service.CreateAsync(hotelDto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("New Hotel");
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Hotel>()), Times.Once);
        _mockRepository.Verify(r => r.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAndReturnHotel()
    {
        // Arrange
        var hotelDto = new HotelDto { Id = 1, Name = "Updated Hotel", Address = "123 St", City = "City", Country = "Country" };
        var existingHotel = new Hotel { Id = 1, Name = "Old Hotel", OwnerId = "user1", Address = "123 St", City = "City", Country = "Country" };
        var updatedHotelDto = new HotelDto { Id = 1, Name = "Updated Hotel", Address = "123 St", City = "City", Country = "Country" };

        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingHotel);
        _mockMapper.Setup(m => m.Map(hotelDto, existingHotel)).Returns(existingHotel);
        _mockRepository.Setup(r => r.Update(It.IsAny<Hotel>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<HotelDto>(existingHotel)).Returns(updatedHotelDto);

        // Act
        var result = await _service.UpdateAsync(1, hotelDto);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Hotel");
        _mockRepository.Verify(r => r.Update(It.IsAny<Hotel>()), Times.Once);
        _mockRepository.Verify(r => r.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldCallRepository()
    {
        // Arrange
        var hotel = new Hotel { Id = 1, Name = "Hotel To Delete", OwnerId = "user1", Address = "123 St", City = "City", Country = "Country" };
        _mockRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(hotel);
        _mockRepository.Setup(r => r.Delete(It.IsAny<Hotel>()));
        _mockRepository.Setup(r => r.SaveAsync()).Returns(Task.CompletedTask);

        // Act
        await _service.DeleteAsync(1);

        // Assert
        _mockRepository.Verify(r => r.GetByIdAsync(1), Times.Once);
        _mockRepository.Verify(r => r.Delete(It.IsAny<Hotel>()), Times.Once);
        _mockRepository.Verify(r => r.SaveAsync(), Times.Once);
    }
}
