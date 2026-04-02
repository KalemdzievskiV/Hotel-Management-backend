using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.Entities;

public class HousekeepingTask
{
    public int Id { get; set; }

    [Required]
    public int RoomId { get; set; }

    [ForeignKey("RoomId")]
    public Room Room { get; set; } = null!;

    public string? AssignedToUserId { get; set; }

    [ForeignKey("AssignedToUserId")]
    public ApplicationUser? AssignedTo { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    [ForeignKey("CreatedByUserId")]
    public ApplicationUser CreatedBy { get; set; } = null!;

    [Required]
    public HousekeepingTaskType Type { get; set; }

    [Required]
    public HousekeepingTaskPriority Priority { get; set; } = HousekeepingTaskPriority.Normal;

    [Required]
    public HousekeepingTaskStatus Status { get; set; } = HousekeepingTaskStatus.Pending;

    public DateTime ScheduledFor { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
