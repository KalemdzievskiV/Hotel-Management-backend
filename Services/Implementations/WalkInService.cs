using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Implementations;

public class WalkInService : IWalkInService
{
    private readonly ApplicationDbContext _context;
    private readonly IReservationService _reservationService;
    private readonly IGuestService _guestService;

    public WalkInService(ApplicationDbContext context, IReservationService reservationService, IGuestService guestService)
    {
        _context = context;
        _reservationService = reservationService;
        _guestService = guestService;
    }

    /// <summary>
    /// Runs all steps in one transaction; ReservationService joins it instead of opening its own.
    /// The in-memory test provider has no transactions, so there the steps just run in order.
    /// </summary>
    private async Task<T> InTransactionAsync<T>(Func<Task<T>> action)
    {
        if (!_context.Database.IsRelational())
            return await action();

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var result = await action();
        await transaction.CommitAsync();
        return result;
    }

    public Task<ReservationDto> QuickCheckInAsync(QuickCheckInDto dto) =>
        InTransactionAsync(async () =>
        {
            var guestId = await ResolveGuestIdAsync(dto);

            // Book without the deposit first: it's validated against the final (discounted) price below
            var reservation = await _reservationService.CreateReservationAsync(new CreateReservationDto
            {
                HotelId = dto.HotelId,
                RoomId = dto.RoomId,
                GuestId = guestId,
                BookingType = dto.BookingType,
                CheckInDate = dto.CheckInDate,
                CheckOutDate = dto.CheckOutDate,
                NumberOfGuests = dto.NumberOfGuests,
                PaymentMethod = dto.PaymentMethod,
                SpecialRequests = dto.SpecialRequests
            });

            if (dto.OverridePrice.HasValue || dto.DiscountAmount > 0)
                reservation = await _reservationService.ApplyPriceAdjustmentAsync(
                    reservation.Id, dto.DiscountAmount, dto.DiscountReason, dto.OverridePrice);

            if (dto.DepositAmount > 0)
                reservation = await _reservationService.RecordPaymentAsync(
                    reservation.Id, dto.DepositAmount, dto.PaymentMethod ?? PaymentMethod.Cash);

            await _reservationService.ConfirmReservationAsync(reservation.Id);
            return await _reservationService.CheckInReservationAsync(reservation.Id);
        });

    public Task<ReservationDto> ExpressCheckOutAsync(int reservationId, ExpressCheckOutDto dto) =>
        InTransactionAsync(async () =>
        {
            if (dto.ExtraCharges > 0)
                await _reservationService.AddExtraChargesAsync(reservationId, dto.ExtraCharges, dto.ExtraChargesNotes);

            if (dto.FinalPayment > 0)
                await _reservationService.RecordPaymentAsync(reservationId, dto.FinalPayment.Value, dto.PaymentMethod ?? PaymentMethod.Cash);

            return await _reservationService.CheckOutReservationAsync(reservationId);
        });

    private async Task<int> ResolveGuestIdAsync(QuickCheckInDto dto)
    {
        if (dto.ExistingGuestId.HasValue)
            return dto.ExistingGuestId.Value;

        if (dto.NewGuest == null)
            throw new ArgumentException("Either ExistingGuestId or NewGuest must be provided");

        // Returning guests are matched by email so their history stays in one profile
        var existing = await _guestService.GetByEmailAsync(dto.NewGuest.Email);
        if (existing != null)
            return existing.Id;

        var created = await _guestService.CreateAsync(new GuestDto
        {
            FirstName = dto.NewGuest.FirstName,
            LastName = dto.NewGuest.LastName,
            Email = dto.NewGuest.Email,
            PhoneNumber = dto.NewGuest.PhoneNumber,
            IdentificationNumber = dto.NewGuest.IdentificationNumber,
            IdentificationType = dto.NewGuest.IdentificationType,
            Nationality = dto.NewGuest.Nationality,
            HotelId = dto.HotelId
        });
        return created.Id;
    }
}
