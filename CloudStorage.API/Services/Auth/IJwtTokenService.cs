using CloudStorage.API.Models;

namespace CloudStorage.API.Services.Auth;

public interface IJwtTokenService
{
    string GenerateAccessToken(ApplicationUser user);
}