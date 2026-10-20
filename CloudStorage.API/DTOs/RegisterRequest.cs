using System.ComponentModel.DataAnnotations;

namespace CloudStorage.API.DTOs;

public record RegisterRequest
{
    [Required] [EmailAddress] public string Email { get; set; } = string.Empty;

    [Required] [MinLength(8)] public string Password { get; set; } = string.Empty;
}