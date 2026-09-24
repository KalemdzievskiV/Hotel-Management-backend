using HotelManagement.Models.DTOs;

namespace HotelManagement.Services.Interfaces;

/// <summary>
/// Front-desk flows that span several steps and must succeed or fail as a whole
/// </summary>
public interface IWalkInService
{
    /// <summary>Resolve/create the guest, book, apply pricing, take the deposit, confirm and check in</summary>
    Task<ReservationDto> QuickCheckInAsync(QuickCheckInDto dto);

    /// <summary>Add extra charges, take the final payment and check out</summary>
    Task<ReservationDto> ExpressCheckOutAsync(int reservationId, ExpressCheckOutDto dto);
}
