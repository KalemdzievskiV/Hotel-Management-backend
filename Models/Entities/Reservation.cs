using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.Entities;

/// <summary>
/// Represents a room reservation/booking
/// </summary>
public class Reservation
{
    public int Id { get; set; }
    
    // Relationships
    [Required]
    public int HotelId { get; set; }
    
    [ForeignKey("HotelId")]
    public Hotel Hotel { get; set; } = null!;
    
    [Required]
    public int RoomId { get; set; }
    
    [ForeignKey("RoomId")]
    public Room Room { get; set; } = null!;
    
    [Required]
    public int GuestId { get; set; }
    
    [ForeignKey("GuestId")]
    public Guest Guest { get; set; } = null!;
    
    // Who created this reservation (Admin/Manager/Guest)
    [Required]
    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
    
    [ForeignKey("CreatedByUserId")]
    public ApplicationUser CreatedBy { get; set; } = null!;
    
    // Booking Details
    [Required]
    public BookingType BookingType { get; set; } = BookingType.Daily;
    
    [Required]
    public DateTime CheckInDate { get; set; }
    
    [Required]
    public DateTime CheckOutDate { get; set; }
    
    // For short-stay bookings (in hours)
    public int? DurationInHours { get; set; }
    
    // Number of guests
    [Required]
    [Range(1, 20)]
    public int NumberOfGuests { get; set; } = 1;
    
    // Reservation Status
    [Required]
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    
    // Financial Details
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    [Range(0, 1000000)]
    public decimal TotalAmount { get; set; } = 0;
    
    [Column(TypeName = "decimal(10,2)")]
    [Range(0, 1000000)]
    public decimal DepositAmount { get; set; } = 0;
    
    [Column(TypeName = "decimal(10,2)")]
    [Range(0, 1000000)]
    public decimal RemainingAmount { get; set; } = 0;
    
    // Payment
    [Required]
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    
    public PaymentMethod? PaymentMethod { get; set; }
    
    [MaxLength(100)]
    public string? PaymentReference { get; set; } // Transaction ID, reference number
    
    // Special Requests & Notes
    [MaxLength(1000)]
    public string? SpecialRequests { get; set; }
    
    [MaxLength(1000)]
    public string? Notes { get; set; } // Internal staff notes
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public DateTime? ConfirmedAt { get; set; }
    
    public DateTime? CheckedInAt { get; set; }
    
    public DateTime? CheckedOutAt { get; set; }
    
    public DateTime? CancelledAt { get; set; }
    
    [MaxLength(500)]
    public string? CancellationReason { get; set; }
    
    // Computed Properties
    [NotMapped]
    public int TotalNights => BookingType == BookingType.Daily 
        ? Math.Max(1, (CheckOutDate.Date - CheckInDate.Date).Days)
        : 0;
    
    [NotMapped]
    public bool IsActive => Status != ReservationStatus.Cancelled 
        && Status != ReservationStatus.CheckedOut 
        && Status != ReservationStatus.NoShow;
    
    [NotMapped]
    public bool CanCheckIn => Status == ReservationStatus.Confirmed 
        && DateTime.UtcNow.Date >= CheckInDate.Date;
    
    [NotMapped]
    public bool CanCheckOut => Status == ReservationStatus.CheckedIn;
    
    [NotMapped]
    public bool CanCancel => Status == ReservationStatus.Pending 
        || Status == ReservationStatus.Confirmed;
}
