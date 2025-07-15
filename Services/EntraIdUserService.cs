using System.Security.Claims;
using Microsoft.Identity.Web;

namespace ArkhamChangeRequest.Services;

public interface IUserService
{
    string GetUserName();
    string GetUserEmail();
    bool IsAuthenticated();
}

public class EntraIdUserService : IUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EntraIdUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetUserName()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return string.Empty;

        // Fallback to claims-based approach for full name
        if (!IsAuthenticated())
        {
            return string.Empty;
        }

        var user = context.User;

        // Try to get the name from the "name" claim (most common for full name)
        var name = user.FindFirst("name")?.Value;
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }

        // Try alternative name claims
        name = user.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }

        // Try to construct from given_name + family_name
        var givenName = user.FindFirst(ClaimTypes.GivenName)?.Value ?? user.FindFirst("given_name")?.Value;
        var familyName = user.FindFirst(ClaimTypes.Surname)?.Value ?? user.FindFirst("family_name")?.Value;
        
        if (!string.IsNullOrEmpty(givenName) && !string.IsNullOrEmpty(familyName))
        {
            return $"{givenName} {familyName}";
        }
        
        // Try just given name if family name is not available
        if (!string.IsNullOrEmpty(givenName))
        {
            return givenName;
        }

        // With Azure App Service Easy Auth, the principal name is usually email
        // Only use as fallback if no proper name claims are found
        var principalName = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].FirstOrDefault();
        if (!string.IsNullOrEmpty(principalName))
        {
            // If it looks like an email, extract the name part before @
            if (principalName.Contains("@"))
            {
                var namePart = principalName.Split('@')[0];
                // Try to make it more readable (replace dots/underscores with spaces and title case)
                return System.Globalization.CultureInfo.CurrentCulture.TextInfo
                    .ToTitleCase(namePart.Replace(".", " ").Replace("_", " "));
            }
            return principalName;
        }
        
        // Final fallback to email
        return GetUserEmail();
    }

    public string GetUserEmail()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return string.Empty;

        // With Azure App Service Easy Auth, try headers first
        var userEmail = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].FirstOrDefault();
        if (!string.IsNullOrEmpty(userEmail) && userEmail.Contains("@"))
        {
            return userEmail;
        }

        // Fallback to claims-based approach
        if (!IsAuthenticated())
        {
            return string.Empty;
        }

        var user = context.User;
        
        // Try preferred_username first (which is usually email in Entra ID)
        var email = user.FindFirst("preferred_username")?.Value;
        if (!string.IsNullOrEmpty(email))
        {
            return email;
        }

        // Try standard email claim
        email = user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value;
        if (!string.IsNullOrEmpty(email))
        {
            return email;
        }

        // Fallback to upn if available
        return user.FindFirst(ClaimTypes.Upn)?.Value ?? string.Empty;
    }

    public bool IsAuthenticated()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return false;

        // With Easy Auth, check for authentication headers
        var clientPrincipal = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].FirstOrDefault();
        if (!string.IsNullOrEmpty(clientPrincipal))
        {
            return true;
        }

        // Fallback to standard authentication check
        return context.User?.Identity?.IsAuthenticated ?? false;
    }
}
