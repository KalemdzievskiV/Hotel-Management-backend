using AutoMapper;
using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HotelManagement.Services.Implementations;

public class ReservationService : IReservationService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReservationService(
        ApplicationDbContext context,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _mapper = mapper;
        _httpContextAccessor = httpContextAccessor;
    }

    private string GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User not authenticated");
    }

    public async Task<ReservationDto> CreateReservationAsync(CreateReservationDto createDto)
    {
        // Validate hotel exists
        var hotel = await _context.Hotels.FindAsync(createDto.HotelId);
        if (hotel == null)
            throw new KeyNotFoundException($"Hotel with ID {createDto.HotelId} not found");

        // Validate room exists and belongs to hotel
        var room = await _context.Rooms
            .Include(r => r.Hotel)
            .FirstOrDefaultAsync(r => r.Id == createDto.RoomId);
        
        if (room == null)
            throw new KeyNotFoundException($"Room with ID {createDto.RoomId} not found");
        
        if (room.HotelId != createDto.HotelId)
            throw new InvalidOperationException($"Room {createDto.RoomId} does not belong to hotel {createDto.HotelId}");

        // Validate guest exists
        var guest = await _context.Guests.FindAsync(createDto.GuestId);
        if (guest == null)
            throw new KeyNotFoundException($"Guest with ID {createDto.GuestId} not found");

        // Validate booking type and room compatibility
        if (createDto.BookingType == BookingType.ShortStay && !room.AllowsShortStay)
            throw new InvalidOperationException($"Room {room.RoomNumber} does not support short-stay bookings");

        // Validate dates
        if (createDto.CheckOutDate <= createDto.CheckInDate)
            throw new InvalidOperationException("Check-out date must be after check-in date");

        // Validate short-stay duration
        if (createDto.BookingType == BookingType.ShortStay)
        {
            var hours = (int)Math.Ceiling((createDto.CheckOutDate - createDto.CheckInDate).TotalHours);
            
            if (room.MinimumShortStayHours.HasValue && hours < room.MinimumShortStayHours.Value)
                throw new InvalidOperationException($"Minimum stay for this room is {room.MinimumShortStayHours} hours");
            
            if (room.MaximumShortStayHours.HasValue && hours > room.MaximumShortStayHours.Value)
                throw new InvalidOperationException($"Maximum stay for this room is {room.MaximumShortStayHours} hours");
            
            createDto.DurationInHours = hours;
        }

        // Check room availability
        var isAvailable = await IsRoomAvailableAsync(createDto.RoomId, createDto.CheckInDate, createDto.CheckOutDate);
        if (!isAvailable)
            throw new InvalidOperationException($"Room {room.RoomNumber} is not available for the selected dates");

        // Validate number of guests against room capacity
        if (createDto.NumberOfGuests > room.Capacity)
            throw new InvalidOperationException($"Room capacity is {room.Capacity} guests, but {createDto.NumberOfGuests} guests requested");

        // Calculate total amount
        decimal totalAmount = CalculatePrice(room, createDto.CheckInDate, createDto.CheckOutDate, createDto.BookingType);

        // Create reservation
        var reservation = new Reservation
        {
            HotelId = createDto.HotelId,
            RoomId = createDto.RoomId,
            GuestId = createDto.GuestId,
            CreatedByUserId = GetCurrentUserId(),
            BookingType = createDto.BookingType,
            CheckInDate = createDto.CheckInDate,
            CheckOutDate = createDto.CheckOutDate,
            DurationInHours = createDto.DurationInHours,
            NumberOfGuests = createDto.NumberOfGuests,
            Status = ReservationStatus.Pending,
            TotalAmount = totalAmount,
            DepositAmount = createDto.DepositAmount,
            RemainingAmount = totalAmount - createDto.DepositAmount,
            PaymentStatus = createDto.DepositAmount > 0 ? PaymentStatus.PartiallyPaid : PaymentStatus.Unpaid,
            PaymentMethod = createDto.PaymentMethod,
            PaymentReference = createDto.PaymentReference,
            SpecialRequests = createDto.SpecialRequests,
            Notes = createDto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        return await GetReservationByIdAsync(reservation.Id)
            ?? throw new InvalidOperationException("Failed to retrieve created reservation");
    }

    private decimal CalculatePrice(Room room, DateTime checkIn, DateTime checkOut, BookingType bookingType)
    {
        if (bookingType == BookingType.ShortStay)
        {
            var hours = (int)Math.Ceiling((checkOut - checkIn).TotalHours);
            return hours * (room.ShortStayHourlyRate ?? 0);
        }
        else
        {
            var nights = Math.Max(1, (checkOut.Date - checkIn.Date).Days);
            return nights * room.PricePerNight;
        }
    }

    public async Task<ReservationDto?> GetReservationByIdAsync(int id)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .Include(r => r.CreatedBy)
            .FirstOrDefaultAsync(r => r.Id == id);

        return reservation == null ? null : MapToDto(reservation);
    }

    public async Task<IEnumerable<ReservationDto>> GetAllReservationsAsync()
    {
        var reservations = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .Include(r => r.CreatedBy)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reservations.Select(MapToDto);
    }

    public async Task<ReservationDto> UpdateReservationAsync(int id, UpdateReservationDto updateDto)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id);
        
        if (reservation == null)
            throw new KeyNotFoundException($"Reservation with ID {id} not found");

        // Only allow updates for pending or confirmed reservations
        if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
            throw new InvalidOperationException($"Cannot update reservation with status {reservation.Status}");

        // Validate dates
        if (updateDto.CheckOutDate <= updateDto.CheckInDate)
            throw new InvalidOperationException("Check-out date must be after check-in date");

        // Check availability if dates changed
        if (reservation.CheckInDate != updateDto.CheckInDate || reservation.CheckOutDate != updateDto.CheckOutDate)
        {
            var isAvailable = await IsRoomAvailableAsync(reservation.RoomId, updateDto.CheckInDate, updateDto.CheckOutDate, id);
            if (!isAvailable)
                throw new InvalidOperationException("Room is not available for the new dates");

            // Recalculate price
            reservation.TotalAmount = CalculatePrice(reservation.Room, updateDto.CheckInDate, updateDto.CheckOutDate, reservation.BookingType);
            reservation.RemainingAmount = reservation.TotalAmount - reservation.DepositAmount;
        }

        // Update fields
        reservation.CheckInDate = updateDto.CheckInDate;
        reservation.CheckOutDate = updateDto.CheckOutDate;
        reservation.DurationInHours = updateDto.DurationInHours;
        reservation.NumberOfGuests = updateDto.NumberOfGuests;
        reservation.DepositAmount = updateDto.DepositAmount;
        reservation.RemainingAmount = reservation.TotalAmount - updateDto.DepositAmount;
        reservation.PaymentMethod = updateDto.PaymentMethod;
        reservation.PaymentReference = updateDto.PaymentReference;
        reservation.SpecialRequests = updateDto.SpecialRequests;
        reservation.Notes = updateDto.Notes;
        reservation.UpdatedAt = DateTime.UtcNow;

        // Update payment status
        if (reservation.DepositAmount >= reservation.TotalAmount)
            reservation.PaymentStatus = PaymentStatus.Paid;
        else if (reservation.DepositAmount > 0)
            reservation.PaymentStatus = PaymentStatus.PartiallyPaid;
        else
            reservation.PaymentStatus = PaymentStatus.Unpaid;

        await _context.SaveChangesAsync();

        return await GetReservationByIdAsync(id)
            ?? throw new InvalidOperationException("Failed to retrieve updated reservation");
    }

    public async Task DeleteReservationAsync(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null)
            throw new KeyNotFoundException($"Reservation with ID {id} not found");

        // Only allow deletion for pending reservations
        if (reservation.Status != ReservationStatus.Pending)
            throw new InvalidOperationException("Only pending reservations can be deleted. Use cancellation for confirmed reservations.");

        _context.Reservations.Remove(reservation);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsByHotelAsync(int hotelId)
    {
        var reservations = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .Include(r => r.CreatedBy)
            .Where(r => r.HotelId == hotelId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reservations.Select(MapToDto);
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsByRoomAsync(int roomId)
    {
        var reservations = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .Include(r => r.CreatedBy)
            .Where(r => r.RoomId == roomId)
            .OrderByDescending(r => r.CheckInDate)
            .ToListAsync();

        return reservations.Select(MapToDto);
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsByGuestAsync(int guestId)
    {
        var reservations = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .Include(r => r.CreatedBy)
            .Where(r => r.GuestId == guestId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reservations.Select(MapToDto);
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsByStatusAsync(ReservationStatus status)
    {
        var reservations = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .Include(r => r.CreatedBy)
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reservations.Select(MapToDto);
    }

    public async Task<IEnumerable<ReservationDto>> GetReservationsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var reservations = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .Include(r => r.CreatedBy)
            .Where(r => r.CheckInDate >= startDate && r.CheckOutDate <= endDate)
            .OrderBy(r => r.CheckInDate)
            .ToListAsync();

        return reservations.Select(MapToDto);
    }

    public async Task<IEnumerable<ReservationDto>> GetUserReservationsAsync(string userId)
    {
        var reservations = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .Include(r => r.CreatedBy)
            .Where(r => r.CreatedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reservations.Select(MapToDto);
    }

    public async Task<bool> IsRoomAvailableAsync(int roomId, DateTime checkIn, DateTime checkOut, int? excludeReservationId = null)
    {
        var conflictingReservations = await _context.Reservations
            .Where(r => r.RoomId == roomId
                && r.Status != ReservationStatus.Cancelled
                && r.Status != ReservationStatus.CheckedOut
                && r.Status != ReservationStatus.NoShow
                && (excludeReservationId == null || r.Id != excludeReservationId)
                && ((r.CheckInDate < checkOut && r.CheckOutDate > checkIn)))
            .ToListAsync();

        return !conflictingReservations.Any();
    }

    public async Task<IEnumerable<ReservationDto>> GetConflictingReservationsAsync(int roomId, DateTime checkIn, DateTime checkOut)
    {
        var reservations = await _context.Reservations
            .Include(r => r.Hotel)
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .Include(r => r.CreatedBy)
            .Where(r => r.RoomId == roomId
                && r.Status != ReservationStatus.Cancelled
                && r.Status != ReservationStatus.CheckedOut
                && r.Status != ReservationStatus.NoShow
                && ((r.CheckInDate < checkOut && r.CheckOutDate > checkIn)))
            .ToListAsync();

        return reservations.Select(MapToDto);
    }

    public async Task<ReservationDto> ConfirmReservationAsync(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null)
            throw new KeyNotFoundException($"Reservation with ID {id} not found");

        if (reservation.Status != ReservationStatus.Pending)
            throw new InvalidOperationException($"Only pending reservations can be confirmed");

        reservation.Status = ReservationStatus.Confirmed;
        reservation.ConfirmedAt = DateTime.UtcNow;
        reservation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetReservationByIdAsync(id)
            ?? throw new InvalidOperationException("Failed to retrieve confirmed reservation");
    }

    public async Task<ReservationDto> CheckInReservationAsync(int id)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id);
        
        if (reservation == null)
            throw new KeyNotFoundException($"Reservation with ID {id} not found");

        if (reservation.Status != ReservationStatus.Confirmed)
            throw new InvalidOperationException($"Only confirmed reservations can be checked in");

        if (DateTime.UtcNow.Date < reservation.CheckInDate.Date)
            throw new InvalidOperationException($"Cannot check in before check-in date");

        reservation.Status = ReservationStatus.CheckedIn;
        reservation.CheckedInAt = DateTime.UtcNow;
        reservation.UpdatedAt = DateTime.UtcNow;

        // Update room status
        reservation.Room.Status = RoomStatus.Occupied;
        reservation.Room.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetReservationByIdAsync(id)
            ?? throw new InvalidOperationException("Failed to retrieve checked-in reservation");
    }

    public async Task<ReservationDto> CheckOutReservationAsync(int id)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id);
        
        if (reservation == null)
            throw new KeyNotFoundException($"Reservation with ID {id} not found");

        if (reservation.Status != ReservationStatus.CheckedIn)
            throw new InvalidOperationException($"Only checked-in reservations can be checked out");

        reservation.Status = ReservationStatus.CheckedOut;
        reservation.CheckedOutAt = DateTime.UtcNow;
        reservation.UpdatedAt = DateTime.UtcNow;

        // Update room status
        reservation.Room.Status = RoomStatus.Cleaning;
        reservation.Room.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetReservationByIdAsync(id)
            ?? throw new InvalidOperationException("Failed to retrieve checked-out reservation");
    }

    public async Task<ReservationDto> CancelReservationAsync(int id, string reason)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null)
            throw new KeyNotFoundException($"Reservation with ID {id} not found");

        if (!reservation.CanCancel)
            throw new InvalidOperationException($"Reservation with status {reservation.Status} cannot be cancelled");

        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancelledAt = DateTime.UtcNow;
        reservation.CancellationReason = reason;
        reservation.UpdatedAt = DateTime.UtcNow;

        // Handle refund if payment was made
        if (reservation.DepositAmount > 0)
        {
            reservation.PaymentStatus = PaymentStatus.Refunding;
        }

        await _context.SaveChangesAsync();

        return await GetReservationByIdAsync(id)
            ?? throw new InvalidOperationException("Failed to retrieve cancelled reservation");
    }

    public async Task<ReservationDto> MarkAsNoShowAsync(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null)
            throw new KeyNotFoundException($"Reservation with ID {id} not found");

        if (reservation.Status != ReservationStatus.Confirmed)
            throw new InvalidOperationException($"Only confirmed reservations can be marked as no-show");

        reservation.Status = ReservationStatus.NoShow;
        reservation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetReservationByIdAsync(id)
            ?? throw new InvalidOperationException("Failed to retrieve no-show reservation");
    }

    public async Task<ReservationDto> RecordPaymentAsync(int id, decimal amount, PaymentMethod paymentMethod, string? reference = null)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null)
            throw new KeyNotFoundException($"Reservation with ID {id} not found");

        if (amount <= 0)
            throw new InvalidOperationException("Payment amount must be greater than 0");

        if (reservation.DepositAmount + amount > reservation.TotalAmount)
            throw new InvalidOperationException($"Payment amount exceeds remaining balance");

        reservation.DepositAmount += amount;
        reservation.RemainingAmount = reservation.TotalAmount - reservation.DepositAmount;
        reservation.PaymentMethod = paymentMethod;
        reservation.PaymentReference = reference;
        reservation.UpdatedAt = DateTime.UtcNow;

        // Update payment status
        if (reservation.DepositAmount >= reservation.TotalAmount)
            reservation.PaymentStatus = PaymentStatus.Paid;
        else if (reservation.DepositAmount > 0)
            reservation.PaymentStatus = PaymentStatus.PartiallyPaid;

        await _context.SaveChangesAsync();

        return await GetReservationByIdAsync(id)
            ?? throw new InvalidOperationException("Failed to retrieve reservation after payment");
    }

    public async Task<ReservationDto> RecordRefundAsync(int id, decimal amount, string? reason = null)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null)
            throw new KeyNotFoundException($"Reservation with ID {id} not found");

        if (amount <= 0)
            throw new InvalidOperationException("Refund amount must be greater than 0");

        if (amount > reservation.DepositAmount)
            throw new InvalidOperationException($"Refund amount cannot exceed paid amount");

        reservation.DepositAmount -= amount;
        reservation.RemainingAmount = reservation.TotalAmount - reservation.DepositAmount;
        reservation.PaymentStatus = PaymentStatus.Refunded;
        reservation.CancellationReason = reason;
        reservation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetReservationByIdAsync(id)
            ?? throw new InvalidOperationException("Failed to retrieve reservation after refund");
    }

    public async Task<int> GetTotalReservationsCountAsync()
    {
        return await _context.Reservations.CountAsync();
    }

    public async Task<decimal> GetTotalRevenueAsync()
    {
        return await _context.Reservations
            .Where(r => r.Status == ReservationStatus.CheckedOut)
            .SumAsync(r => r.TotalAmount);
    }

    public async Task<Dictionary<ReservationStatus, int>> GetReservationCountByStatusAsync()
    {
        var counts = await _context.Reservations
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.ToDictionary(x => x.Status, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetReservationCountByMonthAsync(int year)
    {
        var reservations = await _context.Reservations
            .Where(r => r.CreatedAt.Year == year)
            .ToListAsync();

        return reservations
            .GroupBy(r => r.CreatedAt.ToString("yyyy-MM"))
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private ReservationDto MapToDto(Reservation reservation)
    {
        return new ReservationDto
        {
            Id = reservation.Id,
            HotelId = reservation.HotelId,
            HotelName = reservation.Hotel?.Name,
            RoomId = reservation.RoomId,
            RoomNumber = reservation.Room?.RoomNumber,
            GuestId = reservation.GuestId,
            GuestName = reservation.Guest != null ? $"{reservation.Guest.FirstName} {reservation.Guest.LastName}" : null,
            CreatedByUserId = reservation.CreatedByUserId,
            CreatedByUserName = reservation.CreatedBy?.FullName,
            BookingType = reservation.BookingType,
            CheckInDate = reservation.CheckInDate,
            CheckOutDate = reservation.CheckOutDate,
            DurationInHours = reservation.DurationInHours,
            NumberOfGuests = reservation.NumberOfGuests,
            Status = reservation.Status,
            TotalAmount = reservation.TotalAmount,
            DepositAmount = reservation.DepositAmount,
            RemainingAmount = reservation.RemainingAmount,
            PaymentStatus = reservation.PaymentStatus,
            PaymentMethod = reservation.PaymentMethod,
            PaymentReference = reservation.PaymentReference,
            SpecialRequests = reservation.SpecialRequests,
            Notes = reservation.Notes,
            CreatedAt = reservation.CreatedAt,
            UpdatedAt = reservation.UpdatedAt,
            ConfirmedAt = reservation.ConfirmedAt,
            CheckedInAt = reservation.CheckedInAt,
            CheckedOutAt = reservation.CheckedOutAt,
            CancelledAt = reservation.CancelledAt,
            CancellationReason = reservation.CancellationReason,
            TotalNights = reservation.TotalNights,
            IsActive = reservation.IsActive,
            CanCheckIn = reservation.CanCheckIn,
            CanCheckOut = reservation.CanCheckOut,
            CanCancel = reservation.CanCancel
        };
    }
}
