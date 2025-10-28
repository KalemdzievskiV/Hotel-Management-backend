using System.Security.Claims;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers
{
    public class HotelsController : CrudController<HotelDto>
    {
        private readonly IHotelService _hotelService;

        public HotelsController(IHotelService service) : base(service)
        {
            _hotelService = service;
        }

        /// <summary>
        /// Get all hotels (filtered by ownership for Admins, all for SuperAdmin)
        /// </summary>
        [HttpGet]
        [Authorize]
        public override async Task<IActionResult> GetAllAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
            
            var hotels = await _hotelService.GetAllHotelsForUserAsync(userId, isSuperAdmin);
            return Ok(hotels);
        }

        /// <summary>
        /// Create a new hotel (OwnerId is set from authenticated user)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
        public override async Task<IActionResult> CreateAsync([FromBody] HotelDto dto)
        {
            // Set OwnerId from authenticated user
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            dto.OwnerId = userId;
            
            var created = await _hotelService.CreateAsync(dto);
            return CreatedAtAction("GetById", new { id = created.Id }, created);
        }

        /// <summary>
        /// Update a hotel (only if user is owner or SuperAdmin)
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
        public override async Task<IActionResult> UpdateAsync(int id, [FromBody] HotelDto dto)
        {
            // Get the hotel to check ownership
            var existingHotel = await _hotelService.GetByIdAsync(id);
            
            if (existingHotel == null)
                return NotFound(new { message = $"Hotel with ID {id} not found" });
            
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
            
            // Check if user owns this hotel or is SuperAdmin
            if (!isSuperAdmin && existingHotel.OwnerId != userId)
            {
                return Forbid(); // 403 Forbidden
            }
            
            var updated = await _hotelService.UpdateAsync(id, dto);
            return Ok(updated);
        }

        /// <summary>
        /// Delete a hotel (only if user is owner or SuperAdmin)
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}")]
        public override async Task<IActionResult> DeleteAsync(int id)
        {
            // Get the hotel to check ownership
            var existingHotel = await _hotelService.GetByIdAsync(id);
            
            if (existingHotel == null)
                return NotFound(new { message = $"Hotel with ID {id} not found" });
            
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
            
            // Check if user owns this hotel or is SuperAdmin
            if (!isSuperAdmin && existingHotel.OwnerId != userId)
            {
                return Forbid(); // 403 Forbidden
            }
            
            await _hotelService.DeleteAsync(id);
            return NoContent();
        }
    }
}