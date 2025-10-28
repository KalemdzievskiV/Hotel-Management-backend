using HotelManagement.Models.Entities;

namespace HotelManagement.Services.Interfaces;

public interface ITokenService
{
    string GenerateToken(ApplicationUser user, IList<string> roles);
}
