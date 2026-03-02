using Application.DTOs.Auth;
using Application.Service.Auth;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;

namespace Dashboard.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Login endpoint - validates credentials and returns JWT token
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            // Validate model
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { errors });
            }

            var result = await _authService.LoginAsync(request);

            if (result.IsSuccess)
            {
                _logger.LogInformation($"User {request.Email} logged in successfully");
                return Ok(result.Data);
            }
            else
            {
                _logger.LogWarning($"Login failed for {request.Email}: {result.Error}");
                return Unauthorized(new { message = result.Error });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Login error: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during login" });
        }
    }

    /// <summary>
    /// Logout endpoint
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            // Get user ID from claims
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            await _authService.LogoutAsync(userId);
            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Logout error: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during logout" });
        }
    }

    /// <summary>
    /// Validate token endpoint
    /// </summary>
    [HttpGet("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult ValidateToken()
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var emailClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var nameClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized();
            }

            return Ok(new
            {
                userId = userIdClaim,
                email = emailClaim,
                name = nameClaim,
                role = roleClaim,
                isValid = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Token validation error: {ex.Message}");
            return Unauthorized();
        }
    }
}
