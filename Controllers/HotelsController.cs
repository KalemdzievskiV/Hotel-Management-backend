using System.Security.Claims;
using HotelManagement.Authorization.Requirements;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers
{
    public class HotelsController : CrudController<HotelDto>
    {
        private readonly IHotelService _hotelService;
        private readonly IAuthorizationService _authorizationService;

        public HotelsController(IHotelService service, IAuthorizationService authorizationService) : base(service)
        {
            _hotelService = service;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Get all hotels (automatically filtered by ownership in service layer)
        /// Staff only - shows hotels they manage/own
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "ManagerOrAbove")] // SuperAdmin, Admin, Manager can view hotels
        public override async Task<IActionResult> GetAllAsync()
        {
            // Service layer automatically filters by ownership
            var hotels = await _hotelService.GetAllAsync();
            return Ok(hotels);
        }

        /// <summary>
        /// Get all hotels for public browsing (guests/availability checking)
        /// Returns ALL hotels without ownership filtering
        /// </summary>
        [HttpGet("public")]
        [Authorize] // Any authenticated user (including guests) can view all hotels
        public async Task<IActionResult> GetAllPublicAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isGuest = User.IsInRole(AppRoles.Guest);
            
            // Guests get all hotels, staff get filtered by ownership
            var hotels = isGuest 
                ? await _hotelService.GetAllHotelsUnfilteredAsync()
                : await _hotelService.GetAllAsync();
            
            return Ok(hotels);
        }

        /// <summary>
        /// Create a new hotel (only SuperAdmin and Admin can create)
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "AdminOnly")] // Only SuperAdmin and Admin can create hotels
        public override async Task<IActionResult> CreateAsync([FromBody] HotelDto dto)
        {
            // Check authorization using policy
            var authResult = await _authorizationService.AuthorizeAsync(User, null, new ManageHotelRequirement());
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            // Set OwnerId from authenticated user
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            dto.OwnerId = userId;
            
            var created = await _hotelService.CreateAsync(dto);
            return CreatedAtAction("GetById", new { id = created.Id }, created);
        }

        /// <summary>
        /// Update a hotel (only SuperAdmin and Admin can update their own hotels)
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")] // Only SuperAdmin and Admin can update
        public override async Task<IActionResult> UpdateAsync(int id, [FromBody] HotelDto dto)
        {
            // Get the hotel (service layer filters by ownership)
            var existingHotel = await _hotelService.GetByIdAsync(id);
            
            if (existingHotel == null)
                return NotFound(new { message = $"Hotel with ID {id} not found or you don't have access" });
            
            // Create Hotel entity for authorization check
            var hotel = new Hotel { Id = id, OwnerId = existingHotel.OwnerId };
            
            // Check authorization using resource-based policy
            var authResult = await _authorizationService.AuthorizeAsync(User, hotel, new ManageHotelRequirement());
            if (!authResult.Succeeded)
            {
                return Forbid();
            }
            
            var updated = await _hotelService.UpdateAsync(id, dto);
            return Ok(updated);
        }

        /// <summary>
        /// Delete a hotel (only SuperAdmin and Admin can delete their own hotels)
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")] // Only SuperAdmin and Admin can delete
        public override async Task<IActionResult> DeleteAsync(int id)
        {
            // Get the hotel (service layer filters by ownership)
            var existingHotel = await _hotelService.GetByIdAsync(id);
            
            if (existingHotel == null)
                return NotFound(new { message = $"Hotel with ID {id} not found or you don't have access" });
            
            // Create Hotel entity for authorization check
            var hotel = new Hotel { Id = id, OwnerId = existingHotel.OwnerId };
            
            // Check authorization using resource-based policy
            var authResult = await _authorizationService.AuthorizeAsync(User, hotel, new ManageHotelRequirement());
            if (!authResult.Succeeded)
            {
                return Forbid();
            }
            
            await _hotelService.DeleteAsync(id);
            return NoContent();
        }
    }
}