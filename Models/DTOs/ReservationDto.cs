using System.ComponentModel.DataAnnotations;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// Data Transfer Object for Reservation entity
/// </summary>
public class ReservationDto
{
    public int Id { get; set; }
    
    // Relationships
    [Required(ErrorMessage = "Hotel ID is required")]
    public int HotelId { get; set; }
    
    public string? HotelName { get; set; } // Read-only
    
    [Required(ErrorMessage = "Room ID is required")]
    public int RoomId { get; set; }
    
    public string? RoomNumber { get; set; } // Read-only
    
    [Required(ErrorMessage = "Guest ID is required")]
    public int GuestId { get; set; }
    
    public string? GuestName { get; set; } // Read-only
    
    public string? CreatedByUserId { get; set; } // Read-only (auto-set)
    
    public string? CreatedByUserName { get; set; } // Read-only
    
    // Booking Details
    [Required(ErrorMessage = "Booking type is required")]
    public BookingType BookingType { get; set; } = BookingType.Daily;
    
    [Required(ErrorMessage = "Check-in date is required")]
    public DateTime CheckInDate { get; set; }
    
    [Required(ErrorMessage = "Check-out date is required")]
    public DateTime CheckOutDate { get; set; }
    
    [Range(1, 24, ErrorMessage = "Duration must be between 1 and 24 hours")]
    public int? DurationInHours { get; set; }
    
    [Required(ErrorMessage = "Number of guests is required")]
    [Range(1, 20, ErrorMessage = "Number of guests must be between 1 and 20")]
    public int NumberOfGuests { get; set; } = 1;
    
    // Reservation Status
    [Required(ErrorMessage = "Reservation status is required")]
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    
    // Financial Details
    [Required(ErrorMessage = "Total amount is required")]
    [Range(0, 1000000, ErrorMessage = "Total amount must be between 0 and 1,000,000")]
    public decimal TotalAmount { get; set; } = 0;
    
    [Range(0, 1000000, ErrorMessage = "Deposit amount must be between 0 and 1,000,000")]
    public decimal DepositAmount { get; set; } = 0;
    
    [Range(0, 1000000, ErrorMessage = "Remaining amount must be between 0 and 1,000,000")]
    public decimal RemainingAmount { get; set; } = 0;
    
    // Payment
    [Required(ErrorMessage = "Payment status is required")]
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    
    public PaymentMethod? PaymentMethod { get; set; }
    
    [MaxLength(100, ErrorMessage = "Payment reference cannot exceed 100 characters")]
    public string? PaymentReference { get; set; }
    
    // Special Requests & Notes
    [MaxLength(1000, ErrorMessage = "Special requests cannot exceed 1000 characters")]
    public string? SpecialRequests { get; set; }
    
    [MaxLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; set; }
    
    // Timestamps (Read-only)
    public DateTime CreatedAt { get; set; }
    
    public DateTime? UpdatedAt { get; set; }
    
    public DateTime? ConfirmedAt { get; set; }
    
    public DateTime? CheckedInAt { get; set; }
    
    public DateTime? CheckedOutAt { get; set; }
    
    public DateTime? CancelledAt { get; set; }
    
    [MaxLength(500, ErrorMessage = "Cancellation reason cannot exceed 500 characters")]
    public string? CancellationReason { get; set; }
    
    // Computed Properties (Read-only)
    public int TotalNights { get; set; }
    
    public bool IsActive { get; set; }
    
    public bool CanCheckIn { get; set; }
    
    public bool CanCheckOut { get; set; }
    
    public bool CanCancel { get; set; }
}
