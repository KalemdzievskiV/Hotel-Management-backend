using System.ComponentModel.DataAnnotations;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// DTO for updating an existing reservation
/// </summary>
public class UpdateReservationDto
{
    [Required(ErrorMessage = "Check-in date is required")]
    public DateTime CheckInDate { get; set; }
    
    [Required(ErrorMessage = "Check-out date is required")]
    public DateTime CheckOutDate { get; set; }
    
    [Range(1, 24, ErrorMessage = "Duration must be between 1 and 24 hours")]
    public int? DurationInHours { get; set; }
    
    [Required(ErrorMessage = "Number of guests is required")]
    [Range(1, 20, ErrorMessage = "Number of guests must be between 1 and 20")]
    public int NumberOfGuests { get; set; } = 1;
    
    [Range(0, 1000000, ErrorMessage = "Deposit amount must be between 0 and 1,000,000")]
    public decimal DepositAmount { get; set; } = 0;
    
    public PaymentMethod? PaymentMethod { get; set; }
    
    [MaxLength(100, ErrorMessage = "Payment reference cannot exceed 100 characters")]
    public string? PaymentReference { get; set; }
    
    [MaxLength(1000, ErrorMessage = "Special requests cannot exceed 1000 characters")]
    public string? SpecialRequests { get; set; }
    
    [MaxLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; set; }
}
