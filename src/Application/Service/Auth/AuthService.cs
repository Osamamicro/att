using Application.DTOs.Auth;
using Domain.Entities;
using Domain.Repositories.Users;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Hosting;
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
    private readonly IHostEnvironment _environment;

    public AuthService(
        ILdapAuthService ldapAuthService,
        IUserRepository userRepository,
        IConfiguration configuration,
        ILogger<AuthService> logger,
        IHostEnvironment environment)
    {
        _ldapAuthService = ldapAuthService;
        _userRepository = userRepository;
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
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

            // Check for existing user in database
            var existingUser = await _userRepository.GetByEmailAsync(request.Email);

            // VMS Logic: If user exists in DB, check if they are active before proceeding
            if (existingUser != null && !existingUser.IsActive)
            {
                _logger.LogWarning("Login attempt by inactive user {Email}", request.Email);
                return Result<LoginResponseDto>.Failure("اسم المستخدم غير موجود أو غير نشط"); // User not found or inactive
            }

            bool isAuthenticated = false;
            bool isDevelopment = _environment.IsDevelopment();

            if (isDevelopment)
            {
                // Development ONLY: Allow local password hash fallback (skip LDAP)
                if (existingUser != null && !string.IsNullOrEmpty(existingUser.PasswordHash))
                {
                    isAuthenticated = VerifyPassword(request.Password, existingUser.PasswordHash);
                    _logger.LogInformation("User {Email} authenticated via local password hash (Development mode)", request.Email);
                }
                else
                {
                    // Even in dev, try LDAP if no local hash exists
                    isAuthenticated = await _ldapAuthService.AuthenticateAsync(request.Email, request.Password);
                }
            }
            else
            {
                // UAT & Production: ALWAYS enforce LDAP authentication — no password hash fallback
                isAuthenticated = await _ldapAuthService.AuthenticateAsync(request.Email, request.Password);
                _logger.LogInformation("LDAP authentication enforced for {Email} in {Environment} environment",
                    request.Email, _environment.EnvironmentName);
            }

            if (!isAuthenticated)
                return Result<LoginResponseDto>.Failure("اسم المستخدم / كلمة المرور غير صحيحة"); // Invalid username or password

            // Get or create user
            var user = existingUser;

            if (user == null)
            {
                // Get user details from LDAP (including mobile number for MFA)
                var (firstName, lastName, department, personNumber, mobileNumber) =
                    await _ldapAuthService.GetUserDetailsAsync(request.Email);

                user = new User
                {
                    Email = request.Email,
                    FirstName = firstName ?? request.Email.Split('@')[0],
                    LastName = lastName ?? "",
                    Department = department ?? "",
                    PersonNumber = personNumber ?? "",
                    MobileNumber = mobileNumber ?? "",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Role = "User"
                };

                user = await _userRepository.AddAsync(user);
                await _userRepository.SaveChangesAsync();
                _logger.LogInformation("New user {Email} created from LDAP details", request.Email);
            }
            else
            {
                // Update LDAP details for existing user (sync on each login like VMS)
                var (firstName, lastName, department, personNumber, mobileNumber) =
                    await _ldapAuthService.GetUserDetailsAsync(request.Email);

                if (!string.IsNullOrEmpty(firstName)) user.FirstName = firstName;
                if (!string.IsNullOrEmpty(lastName)) user.LastName = lastName;
                if (!string.IsNullOrEmpty(department)) user.Department = department;
                if (!string.IsNullOrEmpty(personNumber)) user.PersonNumber = personNumber;
                if (!string.IsNullOrEmpty(mobileNumber)) user.MobileNumber = mobileNumber;
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

            _logger.LogInformation("User {Email} logged in successfully", request.Email);
            return Result<LoginResponseDto>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for {Email}", request.Email);
            return Result<LoginResponseDto>.Failure("An error occurred during login. Please try again.");
        }
    }

    public async Task<bool> LogoutAsync(int userId)
    {
        try
        {
            _logger.LogInformation("User {UserId} logged out", userId);
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout error for user {UserId}", userId);
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
            new Claim("PersonNumber", user.PersonNumber ?? ""),
            new Claim(ClaimTypes.MobilePhone, user.MobileNumber ?? "")
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
