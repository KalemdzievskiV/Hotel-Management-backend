using AutoMapper;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Exceptions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HotelManagement.Services.Implementations;

/// <summary>
/// Reservation lifecycle, availability and money.
///
/// Money rules: TotalAmount = room price - DiscountAmount + ExtraCharges. Every payment and
/// refund is a row in the Payments ledger; DepositAmount is the net amount paid so far and
/// is only ever changed by recording a payment or refund.
/// </summary>
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

    private string? TryGetCurrentUserId() =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    #region Transactions

    /// <summary>
    /// Runs the action in a database transaction, joining an outer one if present.
    /// The in-memory test provider has no transactions, so it runs the action directly.
    /// </summary>
    private async Task<T> InTransactionAsync<T>(Func<Task<T>> action)
    {
        if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction != null)
            return await action();

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var result = await action();
        await transaction.CommitAsync();
        return result;
    }

    /// <summary>
    /// Locks the room row until the transaction ends, so two concurrent bookings of the same
    /// room can't both pass the availability check.
    /// </summary>
    private async Task LockRoomAsync(int roomId)
    {
        if (_context.Database.IsRelational())
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Rooms\" WHERE \"Id\" = {roomId} FOR UPDATE");
    }

    #endregion

    #region Create / Update / Delete

    public Task<ReservationDto> CreateReservationAsync(CreateReservationDto createDto) =>
        InTransactionAsync(async () =>
        {
            var hotel = await _context.Hotels.FindAsync(createDto.HotelId)
                ?? throw new KeyNotFoundException($"Hotel with ID {createDto.HotelId} not found");
            if (!hotel.IsActive)
                throw new BusinessRuleException($"{hotel.Name} is not accepting bookings");

            await LockRoomAsync(createDto.RoomId);
            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == createDto.RoomId)
                ?? throw new KeyNotFoundException($"Room with ID {createDto.RoomId} not found");
            if (room.HotelId != createDto.HotelId)
                throw new BusinessRuleException($"Room {createDto.RoomId} does not belong to hotel {createDto.HotelId}");
            if (!room.IsActive || room.Status == RoomStatus.OutOfService)
                throw new BusinessRuleException($"Room {room.RoomNumber} is not available for booking");

            var guest = await _context.Guests.FindAsync(createDto.GuestId)
                ?? throw new KeyNotFoundException($"Guest with ID {createDto.GuestId} not found");
            if (guest.IsBlacklisted)
                throw new BusinessRuleException($"{guest.FirstName} {guest.LastName} is blacklisted and cannot be booked");

            var durationInHours = ValidateStay(room, createDto.CheckInDate, createDto.CheckOutDate, createDto.BookingType, createDto.NumberOfGuests);
            await EnsureAvailableAsync(room, hotel, createDto.CheckInDate, createDto.CheckOutDate, createDto.BookingType);

            var totalAmount = CalculateRoomPrice(room, createDto.CheckInDate, createDto.CheckOutDate, createDto.BookingType);
            if (createDto.DepositAmount > totalAmount)
                throw new BusinessRuleException($"Deposit ({createDto.DepositAmount:0.00}) cannot exceed the total ({totalAmount:0.00})");

            var reservation = new Reservation
            {
                HotelId = createDto.HotelId,
                RoomId = createDto.RoomId,
                GuestId = createDto.GuestId,
                CreatedByUserId = GetCurrentUserId(),
                BookingType = createDto.BookingType,
                CheckInDate = createDto.CheckInDate,
                CheckOutDate = createDto.CheckOutDate,
                DurationInHours = durationInHours,
                NumberOfGuests = createDto.NumberOfGuests,
                Status = ReservationStatus.Pending,
                TotalAmount = totalAmount,
                PaymentMethod = createDto.PaymentMethod,
                PaymentReference = createDto.PaymentReference,
                SpecialRequests = createDto.SpecialRequests,
                Notes = createDto.Notes,
                CreatedAt = DateTime.UtcNow
            };

            if (createDto.DepositAmount > 0)
            {
                reservation.Payments.Add(new Payment
                {
                    Type = PaymentTransactionType.Payment,
                    Amount = createDto.DepositAmount,
                    Method = createDto.PaymentMethod,
                    Reference = createDto.PaymentReference,
                    Notes = "Deposit at booking",
                    CreatedByUserId = reservation.CreatedByUserId
                });
            }

            RecalculateBalance(reservation);
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            return await GetReservationByIdAsync(reservation.Id)
                ?? throw new InvalidOperationException("Failed to retrieve created reservation");
        });

    public Task<ReservationDto> UpdateReservationAsync(int id, UpdateReservationDto updateDto) =>
        InTransactionAsync(async () =>
        {
            var reservation = await _context.Reservations
                .Include(r => r.Room)
                .Include(r => r.Hotel)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new KeyNotFoundException($"Reservation with ID {id} not found");

            // Finished reservations are history: only administrative fields can change
            if (!IsOpen(reservation.Status))
            {
                reservation.Notes = updateDto.Notes;
                reservation.SpecialRequests = updateDto.SpecialRequests;
                reservation.PaymentReference = updateDto.PaymentReference;
                reservation.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return MapToDto(reservation);
            }

            if (reservation.Status == ReservationStatus.CheckedIn && updateDto.CheckInDate != reservation.CheckInDate)
                throw new BusinessRuleException("The guest has already checked in; only the check-out can be changed");

            var datesChanged = reservation.CheckInDate != updateDto.CheckInDate || reservation.CheckOutDate != updateDto.CheckOutDate;
            if (datesChanged)
                await LockRoomAsync(reservation.RoomId);

            var durationInHours = ValidateStay(reservation.Room, updateDto.CheckInDate, updateDto.CheckOutDate, reservation.BookingType, updateDto.NumberOfGuests);

            if (datesChanged)
            {
                await EnsureAvailableAsync(reservation.Room, reservation.Hotel, updateDto.CheckInDate, updateDto.CheckOutDate, reservation.BookingType, reservation.Id);
                var roomPrice = CalculateRoomPrice(reservation.Room, updateDto.CheckInDate, updateDto.CheckOutDate, reservation.BookingType);
                reservation.TotalAmount = Math.Max(0, roomPrice - reservation.DiscountAmount) + reservation.ExtraCharges;
            }

            reservation.CheckInDate = updateDto.CheckInDate;
            reservation.CheckOutDate = updateDto.CheckOutDate;
            reservation.DurationInHours = durationInHours;
            reservation.NumberOfGuests = updateDto.NumberOfGuests;
            reservation.PaymentMethod = updateDto.PaymentMethod;
            reservation.PaymentReference = updateDto.PaymentReference;
            reservation.SpecialRequests = updateDto.SpecialRequests;
            reservation.Notes = updateDto.Notes;
            reservation.UpdatedAt = DateTime.UtcNow;

            RecalculateBalance(reservation);
            await _context.SaveChangesAsync();

            return await GetReservationByIdAsync(id)
                ?? throw new InvalidOperationException("Failed to retrieve updated reservation");
        });

    /// <summary>
    /// Only bookings that haven't started (pending or confirmed) with no money recorded can be
    /// deleted, e.g. one made by mistake; anything else is history and should be cancelled instead.
    /// </summary>
    public async Task DeleteReservationAsync(int id)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException($"Reservation with ID {id} not found");

        if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
            throw new BusinessRuleException("Only reservations that haven't started can be deleted; cancel this reservation instead");

        if (reservation.Payments.Count > 0)
            throw new BusinessRuleException("This reservation has payments recorded; cancel it and refund instead of deleting");

        _context.Reservations.Remove(reservation);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Validation, pricing and availability

    private static bool IsOpen(ReservationStatus status) =>
        status is ReservationStatus.Pending or ReservationStatus.Confirmed or ReservationStatus.CheckedIn;

    /// <summary>
    /// Validates dates, duration and capacity for a stay; returns the short-stay duration in hours
    /// </summary>
    private static int? ValidateStay(Room room, DateTime checkIn, DateTime checkOut, BookingType bookingType, int numberOfGuests)
    {
        if (numberOfGuests > room.Capacity)
            throw new BusinessRuleException($"Room capacity is {room.Capacity} guests, but {numberOfGuests} guests requested");

        if (bookingType == BookingType.Daily)
        {
            if (checkOut.Date <= checkIn.Date)
                throw new BusinessRuleException("Check-out date must be after check-in date for overnight stays");
            return null;
        }

        if (!room.AllowsShortStay)
            throw new BusinessRuleException($"Room {room.RoomNumber} does not support short-stay bookings");

        if (checkOut <= checkIn)
            throw new BusinessRuleException("Check-out time must be after check-in time");

        var hours = (int)Math.Ceiling((checkOut - checkIn).TotalHours);

        if (room.MinimumShortStayHours.HasValue && hours < room.MinimumShortStayHours.Value)
            throw new BusinessRuleException($"Minimum stay for this room is {room.MinimumShortStayHours} hours");

        if (room.MaximumShortStayHours.HasValue && hours > room.MaximumShortStayHours.Value)
            throw new BusinessRuleException($"Maximum stay for this room is {room.MaximumShortStayHours} hours");

        return hours;
    }

    private static decimal CalculateRoomPrice(Room room, DateTime checkIn, DateTime checkOut, BookingType bookingType)
    {
        if (bookingType == BookingType.ShortStay)
        {
            var hours = (int)Math.Ceiling((checkOut - checkIn).TotalHours);
            return hours * (room.ShortStayHourlyRate ?? 0);
        }

        var nights = Math.Max(1, (checkOut.Date - checkIn.Date).Days);
        return nights * room.PricePerNight;
    }

    /// <summary>
    /// Keeps DepositAmount (net paid), RemainingAmount and PaymentStatus consistent with
    /// the payments ledger, the total and the reservation status. Requires Payments loaded.
    /// </summary>
    private static void RecalculateBalance(Reservation reservation)
    {
        var paid = reservation.Payments.Sum(p => p.Type == PaymentTransactionType.Payment ? p.Amount : -p.Amount);
        var hasRefunds = reservation.Payments.Any(p => p.Type == PaymentTransactionType.Refund);

        reservation.DepositAmount = paid;
        reservation.RemainingAmount = Math.Max(0, reservation.TotalAmount - paid);

        if (reservation.Status == ReservationStatus.Cancelled)
            reservation.PaymentStatus = paid > 0 ? PaymentStatus.Refunding
                : hasRefunds ? PaymentStatus.Refunded
                : PaymentStatus.Unpaid;
        else if (paid <= 0)
            reservation.PaymentStatus = hasRefunds ? PaymentStatus.Refunded : PaymentStatus.Unpaid;
        else if (paid >= reservation.TotalAmount)
            reservation.PaymentStatus = PaymentStatus.Paid;
        else
            reservation.PaymentStatus = PaymentStatus.PartiallyPaid;
    }

    /// <summary>
    /// Active reservations of a room that could touch the given window. The two-day margin
    /// covers hotel check-in/out times and cleaning buffers added on top of stored dates.
    /// </summary>
    private Task<List<Reservation>> GetBlockingReservationsAsync(IReadOnlyCollection<int> roomIds, DateTime checkIn, DateTime checkOut, int? excludeReservationId = null)
    {
        var from = checkIn.AddDays(-2);
        var to = checkOut.AddDays(2);

        return _context.Reservations
            .Include(r => r.Guest)
            .Where(r => roomIds.Contains(r.RoomId)
                && r.Status != ReservationStatus.Cancelled
                && r.Status != ReservationStatus.CheckedOut
                && r.Status != ReservationStatus.NoShow
                && (excludeReservationId == null || r.Id != excludeReservationId)
                && r.CheckInDate < to && r.CheckOutDate > from)
            .ToListAsync();
    }

    private async Task EnsureAvailableAsync(Room room, Hotel? hotel, DateTime checkIn, DateTime checkOut, BookingType bookingType, int? excludeReservationId = null)
    {
        hotel ??= await _context.Hotels.FindAsync(room.HotelId);
        var existing = await GetBlockingReservationsAsync(new[] { room.Id }, checkIn, checkOut, excludeReservationId);

        var conflict = StayAvailability.FindConflict(checkIn, checkOut, bookingType, hotel, existing);
        if (conflict != null)
            throw new BusinessRuleException($"Room {room.RoomNumber} is not available for the selected dates. {conflict}");
    }

    public async Task<bool> IsRoomAvailableAsync(int roomId, DateTime checkIn, DateTime checkOut, int? excludeReservationId = null)
    {
        var room = await _context.Rooms.Include(r => r.Hotel).FirstOrDefaultAsync(r => r.Id == roomId);
        if (room == null)
            return false;

        var existing = await GetBlockingReservationsAsync(new[] { roomId }, checkIn, checkOut, excludeReservationId);
        return StayAvailability.FindConflict(checkIn, checkOut, BookingType.Daily, room.Hotel, existing) == null;
    }

    public async Task<IEnumerable<RoomDto>> GetAvailableRoomsAsync(
        int hotelId,
        DateTime checkIn,
        DateTime checkOut,
        BookingType bookingType,
        int? minCapacity = null,
        string? roomType = null)
    {
        var roomsQuery = _context.Rooms
            .Include(r => r.Hotel)
            .Where(r => r.HotelId == hotelId && r.IsActive && r.Status != RoomStatus.OutOfService);

        if (bookingType == BookingType.ShortStay)
            roomsQuery = roomsQuery.Where(r => r.AllowsShortStay);

        if (minCapacity.HasValue)
            roomsQuery = roomsQuery.Where(r => r.Capacity >= minCapacity.Value);

        if (!string.IsNullOrEmpty(roomType) && Enum.TryParse<RoomType>(roomType, true, out var roomTypeEnum))
            roomsQuery = roomsQuery.Where(r => r.Type == roomTypeEnum);

        var rooms = await roomsQuery.ToListAsync();
        if (rooms.Count == 0)
            return Enumerable.Empty<RoomDto>();

        // One query for all candidate rooms instead of one per room
        var reservationsByRoom = (await GetBlockingReservationsAsync(rooms.Select(r => r.Id).ToList(), checkIn, checkOut))
            .ToLookup(r => r.RoomId);

        return rooms
            .Where(room => StayAvailability.FindConflict(checkIn, checkOut, bookingType, room.Hotel, reservationsByRoom[room.Id]) == null)
            .OrderBy(r => r.RoomNumber)
            .Select(r => _mapper.Map<RoomDto>(r))
            .ToList();
    }

    public Task<IEnumerable<ReservationDto>> GetConflictingReservationsAsync(int roomId, DateTime checkIn, DateTime checkOut) =>
        ToDtosAsync(QueryWithDetails()
            .Where(r => r.RoomId == roomId
                && r.Status != ReservationStatus.Cancelled
                && r.Status != ReservationStatus.CheckedOut
                && r.Status != ReservationStatus.NoShow
                && r.CheckInDate < checkOut && r.CheckOutDate > checkIn));

    #endregion

    #region Queries

    /// <summary>
    /// Reservations with the navigation properties ReservationDto needs
    /// </summary>
    private IQueryable<Reservation> QueryWithDetails() => _context.Reservations
        .Include(r => r.Hotel)
        .Include(r => r.Room)
        .Include(r => r.Guest)
        .Include(r => r.CreatedBy);

    private async Task<IEnumerable<ReservationDto>> ToDtosAsync(IQueryable<Reservation> query)
    {
        var reservations = await query.ToListAsync();
        return reservations.Select(MapToDto);
    }

    public async Task<ReservationDto?> GetReservationByIdAsync(int id)
    {
        var reservation = await QueryWithDetails().FirstOrDefaultAsync(r => r.Id == id);
        return reservation == null ? null : MapToDto(reservation);
    }

    public Task<IEnumerable<ReservationDto>> GetReservationsForHotelsAsync(IReadOnlyCollection<int> hotelIds) =>
        ToDtosAsync(QueryWithDetails()
            .Where(r => hotelIds.Contains(r.HotelId))
            .OrderByDescending(r => r.CreatedAt));

    public Task<IEnumerable<ReservationDto>> GetReservationsByHotelAsync(int hotelId) =>
        ToDtosAsync(QueryWithDetails()
            .Where(r => r.HotelId == hotelId)
            .OrderByDescending(r => r.CreatedAt));

    public Task<IEnumerable<ReservationDto>> GetReservationsByRoomAsync(int roomId) =>
        ToDtosAsync(QueryWithDetails()
            .Where(r => r.RoomId == roomId)
            .OrderByDescending(r => r.CheckInDate));

    public Task<IEnumerable<ReservationDto>> GetReservationsByGuestAsync(int guestId, IReadOnlyCollection<int> hotelIds) =>
        ToDtosAsync(QueryWithDetails()
            .Where(r => r.GuestId == guestId && hotelIds.Contains(r.HotelId))
            .OrderByDescending(r => r.CreatedAt));

    public Task<IEnumerable<ReservationDto>> GetReservationsByStatusAsync(ReservationStatus status, IReadOnlyCollection<int> hotelIds) =>
        ToDtosAsync(QueryWithDetails()
            .Where(r => r.Status == status && hotelIds.Contains(r.HotelId))
            .OrderByDescending(r => r.CreatedAt));

    /// <summary>
    /// Reservations overlapping the range (not only those fully inside it)
    /// </summary>
    public Task<IEnumerable<ReservationDto>> GetReservationsByDateRangeAsync(DateTime startDate, DateTime endDate, IReadOnlyCollection<int> hotelIds) =>
        ToDtosAsync(QueryWithDetails()
            .Where(r => hotelIds.Contains(r.HotelId) && r.CheckInDate < endDate && r.CheckOutDate > startDate)
            .OrderBy(r => r.CheckInDate));

    /// <summary>
    /// Reservations for the guest profile linked to this user account, whoever created them
    /// </summary>
    public Task<IEnumerable<ReservationDto>> GetGuestUserReservationsAsync(string userId) =>
        ToDtosAsync(QueryWithDetails()
            .Where(r => r.Guest.UserId == userId)
            .OrderByDescending(r => r.CreatedAt));

    public Task<IEnumerable<ReservationDto>> GetCheckInsOnAsync(DateTime day, IReadOnlyCollection<int> hotelIds)
    {
        var start = day.Date;
        var end = start.AddDays(1);
        return ToDtosAsync(QueryWithDetails()
            .Where(r => hotelIds.Contains(r.HotelId)
                && r.CheckInDate >= start && r.CheckInDate < end
                && (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn))
            .OrderBy(r => r.CheckInDate));
    }

    public Task<IEnumerable<ReservationDto>> GetCheckOutsOnAsync(DateTime day, IReadOnlyCollection<int> hotelIds)
    {
        var start = day.Date;
        var end = start.AddDays(1);
        return ToDtosAsync(QueryWithDetails()
            .Where(r => hotelIds.Contains(r.HotelId)
                && r.CheckOutDate >= start && r.CheckOutDate < end
                && (r.Status == ReservationStatus.CheckedIn || r.Status == ReservationStatus.CheckedOut))
            .OrderBy(r => r.CheckOutDate));
    }

    public async Task<IEnumerable<PaymentDto>> GetPaymentsAsync(int reservationId)
    {
        return await _context.Payments
            .Where(p => p.ReservationId == reservationId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PaymentDto
            {
                Id = p.Id,
                ReservationId = p.ReservationId,
                Type = p.Type,
                Amount = p.Amount,
                Method = p.Method,
                Reference = p.Reference,
                Notes = p.Notes,
                CreatedAt = p.CreatedAt,
                CreatedByName = p.CreatedBy != null ? p.CreatedBy.FirstName + " " + p.CreatedBy.LastName : null
            })
            .ToListAsync();
    }

    #endregion

    #region Status changes

    private async Task<Reservation> LoadForChangeAsync(int id)
    {
        return await _context.Reservations
            .Include(r => r.Room)
            .Include(r => r.Guest)
            .Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException($"Reservation with ID {id} not found");
    }

    private async Task<ReservationDto> SaveAndReloadAsync(Reservation reservation)
    {
        reservation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return await GetReservationByIdAsync(reservation.Id)
            ?? throw new InvalidOperationException($"Failed to retrieve reservation {reservation.Id}");
    }

    public async Task<ReservationDto> ConfirmReservationAsync(int id)
    {
        var reservation = await LoadForChangeAsync(id);

        if (reservation.Status != ReservationStatus.Pending)
            throw new BusinessRuleException("Only pending reservations can be confirmed");

        reservation.Status = ReservationStatus.Confirmed;
        reservation.ConfirmedAt = DateTime.UtcNow;
        return await SaveAndReloadAsync(reservation);
    }

    public async Task<ReservationDto> CheckInReservationAsync(int id)
    {
        var reservation = await LoadForChangeAsync(id);

        if (reservation.Status != ReservationStatus.Confirmed)
            throw new BusinessRuleException("Only confirmed reservations can be checked in");

        if (reservation.Room.Status is RoomStatus.Occupied or RoomStatus.Maintenance or RoomStatus.OutOfService)
            throw new BusinessRuleException($"Room {reservation.Room.RoomNumber} is {reservation.Room.Status} and can't take a guest right now");

        reservation.Status = ReservationStatus.CheckedIn;
        reservation.CheckedInAt = DateTime.UtcNow;
        reservation.Room.Status = RoomStatus.Occupied;
        reservation.Room.UpdatedAt = DateTime.UtcNow;
        return await SaveAndReloadAsync(reservation);
    }

    public async Task<ReservationDto> CheckOutReservationAsync(int id)
    {
        var reservation = await LoadForChangeAsync(id);

        if (reservation.Status != ReservationStatus.CheckedIn)
            throw new BusinessRuleException("Only checked-in reservations can be checked out");

        var now = DateTime.UtcNow;
        reservation.Status = ReservationStatus.CheckedOut;
        reservation.CheckedOutAt = now;
        reservation.Room.Status = RoomStatus.Cleaning;
        reservation.Room.UpdatedAt = now;
        reservation.Guest.LastStayDate = now;
        return await SaveAndReloadAsync(reservation);
    }

    public async Task<ReservationDto> CancelReservationAsync(int id, string reason)
    {
        var reservation = await LoadForChangeAsync(id);

        if (!reservation.CanCancel)
            throw new BusinessRuleException($"Reservation with status {reservation.Status} cannot be cancelled");

        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancelledAt = DateTime.UtcNow;
        reservation.CancellationReason = reason;
        RecalculateBalance(reservation); // anything already paid is now owed back
        return await SaveAndReloadAsync(reservation);
    }

    public async Task<ReservationDto> MarkAsNoShowAsync(int id)
    {
        var reservation = await LoadForChangeAsync(id);

        if (reservation.Status != ReservationStatus.Confirmed)
            throw new BusinessRuleException("Only confirmed reservations can be marked as no-show");

        reservation.Status = ReservationStatus.NoShow;
        return await SaveAndReloadAsync(reservation);
    }

    #endregion

    #region Money

    public async Task<ReservationDto> RecordPaymentAsync(int id, decimal amount, PaymentMethod paymentMethod, string? reference = null)
    {
        var reservation = await LoadForChangeAsync(id);

        if (amount <= 0)
            throw new BusinessRuleException("Payment amount must be greater than 0");

        if (reservation.Status == ReservationStatus.Cancelled)
            throw new BusinessRuleException("Cannot take payments on a cancelled reservation");

        if (amount > reservation.RemainingAmount)
            throw new BusinessRuleException($"Payment amount exceeds remaining balance ({reservation.RemainingAmount:0.00})");

        reservation.Payments.Add(new Payment
        {
            Type = PaymentTransactionType.Payment,
            Amount = amount,
            Method = paymentMethod,
            Reference = reference,
            CreatedByUserId = TryGetCurrentUserId()
        });
        reservation.PaymentMethod = paymentMethod;
        reservation.PaymentReference = reference;
        RecalculateBalance(reservation);
        return await SaveAndReloadAsync(reservation);
    }

    public async Task<ReservationDto> RecordRefundAsync(int id, decimal amount, string? reason = null)
    {
        var reservation = await LoadForChangeAsync(id);

        if (amount <= 0)
            throw new BusinessRuleException("Refund amount must be greater than 0");

        if (amount > reservation.DepositAmount)
            throw new BusinessRuleException($"Refund amount cannot exceed the amount paid ({reservation.DepositAmount:0.00})");

        reservation.Payments.Add(new Payment
        {
            Type = PaymentTransactionType.Refund,
            Amount = amount,
            Method = reservation.PaymentMethod,
            Notes = reason,
            CreatedByUserId = TryGetCurrentUserId()
        });
        RecalculateBalance(reservation);
        return await SaveAndReloadAsync(reservation);
    }

    public async Task<ReservationDto> ApplyPriceAdjustmentAsync(int id, decimal discountAmount, string? reason, decimal? overridePrice)
    {
        var reservation = await LoadForChangeAsync(id);

        if (!IsOpen(reservation.Status))
            throw new BusinessRuleException("Prices can only be adjusted on open reservations");

        var roomPrice = CalculateRoomPrice(reservation.Room, reservation.CheckInDate, reservation.CheckOutDate, reservation.BookingType);

        if (overridePrice.HasValue)
        {
            if (overridePrice.Value < 0 || overridePrice.Value > roomPrice)
                throw new BusinessRuleException($"Override price must be between 0 and the room price ({roomPrice:0.00})");
            // Stored as a discount so the room price and the markdown both stay visible
            discountAmount = roomPrice - overridePrice.Value;
            reason ??= $"Price override to {overridePrice.Value:0.00}";
        }

        if (discountAmount < 0 || discountAmount > roomPrice)
            throw new BusinessRuleException($"Discount must be between 0 and the room price ({roomPrice:0.00})");

        reservation.DiscountAmount = discountAmount;
        reservation.DiscountReason = reason;
        reservation.TotalAmount = roomPrice - discountAmount + reservation.ExtraCharges;

        if (reservation.DepositAmount > reservation.TotalAmount)
            throw new BusinessRuleException("The new total is below what has already been paid; refund the difference first");

        RecalculateBalance(reservation);
        return await SaveAndReloadAsync(reservation);
    }

    public async Task<ReservationDto> AddExtraChargesAsync(int id, decimal amount, string? notes)
    {
        var reservation = await LoadForChangeAsync(id);

        if (amount <= 0)
            throw new BusinessRuleException("Extra charges must be greater than 0");

        if (reservation.Status != ReservationStatus.CheckedIn)
            throw new BusinessRuleException("Extra charges can only be added while the guest is checked in");

        reservation.ExtraCharges += amount;
        reservation.ExtraChargesNotes = string.IsNullOrWhiteSpace(reservation.ExtraChargesNotes)
            ? notes
            : $"{reservation.ExtraChargesNotes}; {notes}";
        reservation.TotalAmount += amount;
        RecalculateBalance(reservation);
        return await SaveAndReloadAsync(reservation);
    }

    #endregion

    #region Statistics

    public Task<int> GetTotalReservationsCountAsync(IReadOnlyCollection<int> hotelIds) =>
        _context.Reservations.CountAsync(r => hotelIds.Contains(r.HotelId));

    public Task<decimal> GetTotalRevenueAsync(IReadOnlyCollection<int> hotelIds) =>
        _context.Reservations
            .Where(r => hotelIds.Contains(r.HotelId) && r.Status == ReservationStatus.CheckedOut)
            .SumAsync(r => r.TotalAmount);

    public async Task<Dictionary<ReservationStatus, int>> GetReservationCountByStatusAsync(IReadOnlyCollection<int> hotelIds)
    {
        var counts = await _context.Reservations
            .Where(r => hotelIds.Contains(r.HotelId))
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.ToDictionary(x => x.Status, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetReservationCountByMonthAsync(int year, IReadOnlyCollection<int> hotelIds)
    {
        var counts = await _context.Reservations
            .Where(r => hotelIds.Contains(r.HotelId) && r.CreatedAt.Year == year)
            .GroupBy(r => r.CreatedAt.Month)
            .Select(g => new { Month = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.OrderBy(x => x.Month).ToDictionary(x => $"{year}-{x.Month:D2}", x => x.Count);
    }

    #endregion

    private static ReservationDto MapToDto(Reservation reservation)
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
            DiscountAmount = reservation.DiscountAmount,
            DiscountReason = reservation.DiscountReason,
            ExtraCharges = reservation.ExtraCharges,
            ExtraChargesNotes = reservation.ExtraChargesNotes,
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
