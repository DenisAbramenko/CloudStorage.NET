using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CloudStorage.API.Models;
using Microsoft.IdentityModel.Tokens;

namespace CloudStorage.API.Services.Auth;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(ApplicationUser user)
    {
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var secretKey = _configuration["Jwt:Secret"];
        var expirationMinutes = _configuration.GetValue<int>("Jwt:AccessTokenExpirationMinutes");

        if (string.IsNullOrWhiteSpace(issuer)
            || string.IsNullOrWhiteSpace(audience)
            || string.IsNullOrWhiteSpace(secretKey)
            || expirationMinutes <= 0)
            throw new InvalidOperationException("Invalid JWT token configuration");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}