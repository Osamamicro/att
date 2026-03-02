using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Service.Auth;

public interface ILdapAuthService
{
    Task<bool> AuthenticateAsync(string email, string password);
    Task<(string FirstName, string LastName, string Department, string PersonNumber, string MobileNumber)> GetUserDetailsAsync(string email);
}

public class LdapAuthService : ILdapAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LdapAuthService> _logger;

    public LdapAuthService(IConfiguration configuration, ILogger<LdapAuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user against LDAP/Active Directory using PrincipalContext.ValidateCredentials
    /// (same approach as VMS production login).
    /// </summary>
    public async Task<bool> AuthenticateAsync(string email, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return false;

            var ldapPath = _configuration["Ldap:Path"];
            var domain = _configuration["Ldap:Domain"];

            if (string.IsNullOrWhiteSpace(ldapPath) || string.IsNullOrWhiteSpace(domain))
            {
                _logger.LogWarning("LDAP configuration is missing (Path or Domain)");
                return false;
            }

            // Extract username from email (assuming email format: username@domain)
            var username = email.Split('@')[0];

            // Sanitize username to prevent LDAP injection
            username = SanitizeLdapInput(username);

            return await Task.Run(() =>
            {
                try
                {
                    // Step 1: Verify user exists in LDAP directory first (like VMS)
                    using var entry = new DirectoryEntry(ldapPath);
                    using var searcher = new DirectorySearcher(entry);
                    searcher.Filter = $"(SAMAccountName={username})";
                    searcher.PropertiesToLoad.Add("cn");
                    searcher.PropertiesToLoad.Add("mail");
                    searcher.PropertiesToLoad.Add("mobile");

                    var searchResult = searcher.FindOne();
                    if (searchResult == null)
                    {
                        _logger.LogWarning("LDAP authentication failed for user {Email}: User not found in directory", email);
                        return false;
                    }

                    // Step 2: Validate credentials using PrincipalContext (same as VMS production)
                    using var context = new PrincipalContext(ContextType.Domain);
                    var isValid = context.ValidateCredentials(username, password);

                    if (isValid)
                    {
                        _logger.LogInformation("User {Email} authenticated successfully via LDAP", email);
                    }
                    else
                    {
                        _logger.LogWarning("LDAP authentication failed for user {Email}: Invalid credentials", email);
                    }

                    return isValid;
                }
                catch (PrincipalServerDownException ex)
                {
                    _logger.LogError(ex, "LDAP server is unreachable for user {Email}", email);
                    return false;
                }
                catch (DirectoryServicesCOMException ex)
                {
                    _logger.LogError(ex, "LDAP directory services error for user {Email}: {Message}", email, ex.Message);
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LDAP authentication error for user {Email}: {Message}", email, ex.Message);
                    return false;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AuthenticateAsync for {Email}", email);
            return false;
        }
    }

    /// <summary>
    /// Retrieves user details from LDAP/Active Directory including mobile number (for MFA).
    /// Retrieves: givenName, sn, department, employeeID, mobile, mail, cn
    /// </summary>
    public async Task<(string FirstName, string LastName, string Department, string PersonNumber, string MobileNumber)> GetUserDetailsAsync(string email)
    {
        try
        {
            var ldapPath = _configuration["Ldap:Path"];
            var adminUsername = _configuration["Ldap:AdminUsername"];
            var adminPassword = _configuration["Ldap:AdminPassword"];

            if (string.IsNullOrWhiteSpace(ldapPath))
            {
                _logger.LogWarning("LDAP Path configuration is missing");
                return ("", "", "", "", "");
            }

            var username = email.Split('@')[0];
            username = SanitizeLdapInput(username);

            return await Task.Run(() =>
            {
                try
                {
                    using var entry = string.IsNullOrWhiteSpace(adminUsername)
                        ? new DirectoryEntry(ldapPath)
                        : new DirectoryEntry(ldapPath, adminUsername, adminPassword);
                    using var searcher = new DirectorySearcher(entry);

                    searcher.Filter = $"(sAMAccountName={username})";
                    searcher.PropertiesToLoad.AddRange(new[]
                    {
                        "givenName", "sn", "department", "mail",
                        "employeeID", "mobile", "cn"
                    });

                    var result = searcher.FindOne();
                    if (result != null)
                    {
                        var firstName = GetLdapProperty(result, "givenName");
                        var lastName = GetLdapProperty(result, "sn");
                        var department = GetLdapProperty(result, "department");
                        var personNumber = GetLdapProperty(result, "employeeID");
                        var mobileRaw = GetLdapProperty(result, "mobile");

                        // Extract last 9 digits of mobile number (same as VMS)
                        var mobileNumber = !string.IsNullOrEmpty(mobileRaw) && mobileRaw.Length >= 9
                            ? mobileRaw.Substring(mobileRaw.Length - 9)
                            : mobileRaw;

                        _logger.LogInformation("Retrieved LDAP details for user {Email}: Name={FirstName} {LastName}, Dept={Department}",
                            email, firstName, lastName, department);

                        return (firstName, lastName, department, personNumber, mobileNumber);
                    }

                    _logger.LogWarning("No LDAP entry found for user {Email}", email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving user details from LDAP for {Email}: {Message}", email, ex.Message);
                }

                return ("", "", "", "", "");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUserDetailsAsync for {Email}", email);
            return ("", "", "", "", "");
        }
    }

    /// <summary>
    /// Safely retrieves a property value from an LDAP search result.
    /// </summary>
    private static string GetLdapProperty(SearchResult result, string propertyName)
    {
        try
        {
            if (result.Properties.Contains(propertyName) && result.Properties[propertyName].Count > 0)
            {
                var value = result.Properties[propertyName][0];
                return value?.ToString() ?? string.Empty;
            }
        }
        catch (Exception)
        {
            // Silently continue - property not available
        }

        return string.Empty;
    }

    /// <summary>
    /// Sanitizes input to prevent LDAP injection attacks.
    /// Escapes special LDAP filter characters.
    /// </summary>
    private static string SanitizeLdapInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Escape LDAP special characters per RFC 4515
        var sanitized = input
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");

        return sanitized;
    }
}
