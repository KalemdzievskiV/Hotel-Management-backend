using HotelManagement.Models.Enums;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// One entry of a reservation's payment ledger
/// </summary>
public class PaymentDto
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public PaymentTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod? Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
}
