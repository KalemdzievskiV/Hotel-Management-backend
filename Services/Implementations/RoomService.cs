using AutoMapper;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Repositories.Interfaces;
using HotelManagement.Services.Interfaces;

namespace HotelManagement.Services.Implementations;

public class RoomService : CrudService<Room, RoomDto>, IRoomService
{
    private readonly IGenericRepository<Room> _roomRepository;
    private readonly IMapper _mapper;

    public RoomService(IGenericRepository<Room> repository, IMapper mapper) 
        : base(repository, mapper)
    {
        _roomRepository = repository;
        _mapper = mapper;
    }

    public override async Task<RoomDto> CreateAsync(RoomDto dto)
    {
        // Validate room number uniqueness
        var isUnique = await IsRoomNumberUniqueAsync(dto.HotelId, dto.RoomNumber);
        if (!isUnique)
        {
            throw new InvalidOperationException($"Room number '{dto.RoomNumber}' already exists in this hotel");
        }

        var room = _mapper.Map<Room>(dto);
        room.CreatedAt = DateTime.UtcNow;
        
        await _roomRepository.AddAsync(room);
        await _roomRepository.SaveAsync();
        
        return _mapper.Map<RoomDto>(room);
    }

    public override async Task<RoomDto> UpdateAsync(int id, RoomDto dto)
    {
        var existingRoom = await _roomRepository.GetByIdAsync(id);
        if (existingRoom == null)
            throw new KeyNotFoundException($"Room with ID {id} not found");

        // Validate room number uniqueness (excluding current room)
        if (existingRoom.RoomNumber != dto.RoomNumber)
        {
            var isUnique = await IsRoomNumberUniqueAsync(dto.HotelId, dto.RoomNumber, id);
            if (!isUnique)
            {
                throw new InvalidOperationException($"Room number '{dto.RoomNumber}' already exists in this hotel");
            }
        }

        // Don't allow changing HotelId
        dto.HotelId = existingRoom.HotelId;

        _mapper.Map(dto, existingRoom);
        existingRoom.UpdatedAt = DateTime.UtcNow;
        
        _roomRepository.Update(existingRoom);
        await _roomRepository.SaveAsync();
        
        return _mapper.Map<RoomDto>(existingRoom);
    }

    public async Task<IEnumerable<RoomDto>> GetRoomsByHotelIdAsync(int hotelId)
    {
        var rooms = await _roomRepository.FindAsync(r => r.HotelId == hotelId);
        return _mapper.Map<IEnumerable<RoomDto>>(rooms);
    }

    public async Task<IEnumerable<RoomDto>> GetRoomsByHotelAndStatusAsync(int hotelId, RoomStatus status)
    {
        var rooms = await _roomRepository.FindAsync(r => r.HotelId == hotelId && r.Status == status);
        return _mapper.Map<IEnumerable<RoomDto>>(rooms);
    }

    public async Task<IEnumerable<RoomDto>> GetAvailableRoomsByHotelAsync(int hotelId)
    {
        var rooms = await _roomRepository.FindAsync(r => 
            r.HotelId == hotelId && 
            r.IsActive && 
            r.Status == RoomStatus.Available);
        return _mapper.Map<IEnumerable<RoomDto>>(rooms);
    }

    public async Task<RoomDto> UpdateRoomStatusAsync(int roomId, RoomStatus newStatus)
    {
        var room = await _roomRepository.GetByIdAsync(roomId);
        if (room == null)
            throw new KeyNotFoundException($"Room with ID {roomId} not found");

        room.Status = newStatus;
        room.UpdatedAt = DateTime.UtcNow;
        
        _roomRepository.Update(room);
        await _roomRepository.SaveAsync();
        
        return _mapper.Map<RoomDto>(room);
    }

    public async Task<bool> IsRoomNumberUniqueAsync(int hotelId, string roomNumber, int? excludeRoomId = null)
    {
        var rooms = await _roomRepository.FindAsync(r => 
            r.HotelId == hotelId && 
            r.RoomNumber == roomNumber);

        if (excludeRoomId.HasValue)
        {
            rooms = rooms.Where(r => r.Id != excludeRoomId.Value);
        }

        return !rooms.Any();
    }

    public async Task MarkRoomAsCleanedAsync(int roomId)
    {
        var room = await _roomRepository.GetByIdAsync(roomId);
        if (room == null)
            throw new KeyNotFoundException($"Room with ID {roomId} not found");

        room.LastCleaned = DateTime.UtcNow;
        room.Status = RoomStatus.Available; // Mark as available after cleaning
        room.UpdatedAt = DateTime.UtcNow;
        
        _roomRepository.Update(room);
        await _roomRepository.SaveAsync();
    }

    public async Task RecordMaintenanceAsync(int roomId, string notes)
    {
        var room = await _roomRepository.GetByIdAsync(roomId);
        if (room == null)
            throw new KeyNotFoundException($"Room with ID {roomId} not found");

        room.LastMaintenance = DateTime.UtcNow;
        room.Status = RoomStatus.Maintenance;
        room.Notes = notes;
        room.UpdatedAt = DateTime.UtcNow;
        
        _roomRepository.Update(room);
        await _roomRepository.SaveAsync();
    }
}
