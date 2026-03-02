using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth;

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    [MaxLength(255, ErrorMessage = "Password cannot exceed 255 characters")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
