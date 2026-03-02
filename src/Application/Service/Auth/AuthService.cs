using Application.DTOs.Auth;
using Domain.Entities;
using Domain.Repositories.Users;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Common;

namespace Application.Service.Auth;

public interface IAuthService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request);
    Task<bool> LogoutAsync(int userId);
}

public class AuthService : IAuthService
{
    private readonly ILdapAuthService _ldapAuthService;
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ILdapAuthService ldapAuthService,
        IUserRepository userRepository,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _ldapAuthService = ldapAuthService;
        _userRepository = userRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Result<LoginResponseDto>.Failure("Email and password are required");

            // Validate email format
            if (!IsValidEmail(request.Email))
                return Result<LoginResponseDto>.Failure("Invalid email format");

            // Check for existing user with password hash (development mode)
            var existingUser = await _userRepository.GetByEmailAsync(request.Email);
            bool isAuthenticated = false;

            if (existingUser != null && !string.IsNullOrEmpty(existingUser.PasswordHash))
            {
                // Verify password hash (simple comparison for development)
                isAuthenticated = VerifyPassword(request.Password, existingUser.PasswordHash);
            }
            else
            {
                // Attempt LDAP authentication
                isAuthenticated = await _ldapAuthService.AuthenticateAsync(request.Email, request.Password);
            }

            if (!isAuthenticated)
                return Result<LoginResponseDto>.Failure("Invalid email or password");

            // Get or create user  
            var user = existingUser ?? await _userRepository.GetByEmailAsync(request.Email);
            
            if (user == null)
            {
                // Get user details from LDAP
                var (firstName, lastName, department, personNumber) = await _ldapAuthService.GetUserDetailsAsync(request.Email);
                
                user = new User
                {
                    Email = request.Email,
                    FirstName = firstName ?? request.Email.Split('@')[0],
                    LastName = lastName ?? "",
                    Department = department ?? "",
                    PersonNumber = personNumber ?? "",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Role = "User"
                };

                user = await _userRepository.AddAsync(user);
                await _userRepository.SaveChangesAsync();
            }

            // Update last login
            await _userRepository.UpdateLastLoginAsync(user.Id);
            await _userRepository.SaveChangesAsync();

            // Generate JWT token
            var token = GenerateJwtToken(user);
            var response = new LoginResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PersonNumber = user.PersonNumber,
                Role = user.Role,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            _logger.LogInformation($"User {request.Email} logged in successfully");
            return Result<LoginResponseDto>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Login error: {ex.Message}");
            return Result<LoginResponseDto>.Failure("An error occurred during login. Please try again.");
        }
    }

    public async Task<bool> LogoutAsync(int userId)
    {
        try
        {
            _logger.LogInformation($"User {userId} logged out");
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Logout error: {ex.Message}");
            return false;
        }
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"];
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];

        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("JWT SecretKey is not configured");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("Department", user.Department ?? ""),
            new Claim("PersonNumber", user.PersonNumber ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        // Simple hash comparison for development mode
        // In production, use BCrypt or similar
        return HashPassword(password) == passwordHash;
    }

    public static string HashPassword(string password)
    {
        // Simple hash for development - use BCrypt in production
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
