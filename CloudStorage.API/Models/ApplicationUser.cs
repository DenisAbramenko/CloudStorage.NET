using Microsoft.AspNetCore.Identity;

namespace CloudStorage.API.Models;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}