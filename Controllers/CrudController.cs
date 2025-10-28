using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HotelManagement.Models.Constants;
using HotelManagement.Services.Interfaces;

namespace HotelManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public abstract class CrudController<TDto> : ControllerBase
    {
        private readonly ICrudService<TDto> _service;

        protected CrudController(ICrudService<TDto> service)
        {
            _service = service;
        }

        [HttpGet]
        [Authorize] // Requires authentication
        public virtual async Task<IActionResult> GetAllAsync() => Ok(await _service.GetAllAsync());

        [HttpGet("{id:int}")]
        [Authorize] // Requires authentication
        public virtual async Task<IActionResult> GetByIdAsync(int id)
        {
            var dto = await _service.GetByIdAsync(id);
            return dto == null ? NotFound() : Ok(dto);
        }

        [HttpPost]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")] // Management roles can create
        public virtual async Task<IActionResult> CreateAsync([FromBody] TDto dto)
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction("GetById", new { id = (created as dynamic)?.Id }, created);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")] // Management roles can update
        public virtual async Task<IActionResult> UpdateAsync(int id, [FromBody] TDto dto)
        {
            var updated = await _service.UpdateAsync(id, dto);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}")] // Only SuperAdmin and Admin can delete
        public virtual async Task<IActionResult> DeleteAsync(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
    }
}