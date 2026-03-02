namespace Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string PersonNumber { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public string? PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
