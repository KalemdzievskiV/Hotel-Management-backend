using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.Entities;

/// <summary>
/// One money movement on a reservation. Reservation.DepositAmount is the running
/// total of these (payments minus refunds) and is kept in sync by ReservationService.
/// </summary>
public class Payment
{
    public int Id { get; set; }

    [Required]
    public int ReservationId { get; set; }

    [ForeignKey("ReservationId")]
    public Reservation Reservation { get; set; } = null!;

    [Required]
    public PaymentTransactionType Type { get; set; } = PaymentTransactionType.Payment;

    /// <summary>
    /// Always positive; Type says whether money came in or went out
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    public PaymentMethod? Method { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    [ForeignKey("CreatedByUserId")]
    public ApplicationUser? CreatedBy { get; set; }
}
