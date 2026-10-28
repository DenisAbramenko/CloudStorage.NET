using CloudStorage.API.DTOs;
using CloudStorage.API.Models;
using CloudStorage.API.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CloudStorage.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(UserManager<ApplicationUser> userManager, IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterRequest request)
    {
        var user = new ApplicationUser();
        user.Email = request.Email;
        user.UserName = request.Email;
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("register", error.Description);
            return ValidationProblem(ModelState);
        }

        return Ok("User register successfully");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null) return InvalidCredentials();
        var resultPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!resultPassword) return InvalidCredentials();
        var token = _jwtTokenService.GenerateAccessToken(user);
        return Ok(new { access_token = token });
    }

    private ObjectResult InvalidCredentials()
    {
        return Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: "Неверный email или пароль.");
    }
}