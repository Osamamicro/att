using System.DirectoryServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Service.Auth;

public interface ILdapAuthService
{
    Task<bool> AuthenticateAsync(string email, string password);
    Task<(string FirstName, string LastName, string Department, string PersonNumber)> GetUserDetailsAsync(string email);
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
    /// Authenticates a user against LDAP/Active Directory
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
                _logger.LogWarning("LDAP configuration is missing");
                return false;
            }

            // Extract username from email (assuming email format: username@domain)
            var username = email.Split('@')[0];

            return await Task.Run(() =>
            {
                try
                {
                    using (var entry = new DirectoryEntry(ldapPath, $"{domain}\\{username}", password))
                    {
                        // Test the connection by binding
                        _ = entry.Properties["name"]?.Value;
                        _logger.LogInformation($"User {email} authenticated successfully");
                        return true;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogWarning($"LDAP authentication failed for user {email}: Invalid credentials");
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"LDAP authentication error for user {email}: {ex.Message}");
                    return false;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in AuthenticateAsync: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Retrieves user details from LDAP/Active Directory
    /// </summary>
    public async Task<(string FirstName, string LastName, string Department, string PersonNumber)> GetUserDetailsAsync(string email)
    {
        try
        {
            var ldapPath = _configuration["Ldap:Path"];
            var adminUsername = _configuration["Ldap:AdminUsername"];
            var adminPassword = _configuration["Ldap:AdminPassword"];

            if (string.IsNullOrWhiteSpace(ldapPath))
                return ("", "", "", "");

            var username = email.Split('@')[0];

            return await Task.Run(() =>
            {
                try
                {
                    using (var entry = new DirectoryEntry(ldapPath, adminUsername, adminPassword))
                    using (var searcher = new DirectorySearcher(entry))
                    {
                        searcher.Filter = $"(sAMAccountName={username})";
                        searcher.PropertiesToLoad.AddRange(new[] { "givenName", "sn", "department", "mail", "employeeID" });

                        var result = searcher.FindOne();
                        if (result != null)
                        {
                            var firstName = GetLdapProperty(result, "givenName");
                            var lastName = GetLdapProperty(result, "sn");
                            var department = GetLdapProperty(result, "department");
                            var personNumber = GetLdapProperty(result, "employeeID");

                            return (firstName, lastName, department, personNumber);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error retrieving user details from LDAP for {email}: {ex.Message}");
                }

                return ("", "", "", "");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in GetUserDetailsAsync: {ex.Message}");
            return ("", "", "", "");
        }
    }

    private static string GetLdapProperty(SearchResult result, string propertyName)
    {
        try
        {
            if (result.Properties.Contains(propertyName))
            {
                var value = result.Properties[propertyName][0];
                return value?.ToString() ?? string.Empty;
            }
        }
        catch (Exception)
        {
            // Log and continue
        }

        return string.Empty;
    }
}
