using System.ComponentModel.DataAnnotations;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.DTOs;

public class QuickCheckInDto
{
    [Required]
    public int HotelId { get; set; }

    [Required]
    public int RoomId { get; set; }

    public int? ExistingGuestId { get; set; }

    public QuickGuestDto? NewGuest { get; set; }

    [Required]
    public DateTime CheckInDate { get; set; }

    [Required]
    public DateTime CheckOutDate { get; set; }

    public BookingType BookingType { get; set; } = BookingType.Daily;

    public int? DurationInHours { get; set; }

    public int NumberOfGuests { get; set; } = 1;

    public decimal? OverridePrice { get; set; }

    public decimal DiscountAmount { get; set; } = 0;

    [MaxLength(200)]
    public string? DiscountReason { get; set; }

    public decimal DepositAmount { get; set; } = 0;

    public PaymentMethod? PaymentMethod { get; set; }

    [MaxLength(1000)]
    public string? SpecialRequests { get; set; }
}

public class QuickGuestDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? IdentificationNumber { get; set; }

    [MaxLength(50)]
    public string? IdentificationType { get; set; }

    [MaxLength(100)]
    public string? Nationality { get; set; }
}

public class ExpressCheckOutDto
{
    public decimal ExtraCharges { get; set; } = 0;

    [MaxLength(500)]
    public string? ExtraChargesNotes { get; set; }

    public decimal? FinalPayment { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }
}

public class GuestIntelligenceDto
{
    public int GuestId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsVIP { get; set; }
    public bool IsBlacklisted { get; set; }
    public string? BlacklistReason { get; set; }
    public string? Preferences { get; set; }
    public string? SpecialRequests { get; set; }
    public string? Notes { get; set; }
    public int TotalStays { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastStayDate { get; set; }
    public string? MostUsedRoomType { get; set; }
    public bool HasOutstandingPayments { get; set; }
    public List<GuestStaySummaryDto> RecentStays { get; set; } = new();
}

public class GuestStaySummaryDto
{
    public int ReservationId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class UpdateGuestFlagsDto
{
    public bool? IsVIP { get; set; }
    public bool? IsBlacklisted { get; set; }

    [MaxLength(500)]
    public string? BlacklistReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
