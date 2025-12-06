using System.Linq;
using System.Security.Claims;

namespace ArkhamChangeRequest.Services;

public interface IUserService
{
    string GetUserName();
    string GetUserEmail();
    bool IsAuthenticated();
}

public class ClaimsUserService : IUserService
{
    private static readonly string[] EmailClaimTypes =
    {
        ClaimTypes.Email,
        "email",
        "preferred_username",
        ClaimTypes.Upn,
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"
    };

    private static readonly string[] NameClaimTypes =
    {
        "http://schemas.microsoft.com/identity/claims/displayname",
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/displayname",
        "displayName",
        "full_name",
        "name",
        ClaimTypes.Name,
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
    };

    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClaimsUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetUserName()
    {
        var user = GetPrincipal();
        if (user == null)
        {
            return string.Empty;
        }

        var name = NameClaimTypes
            .Select(type => user.FindFirst(type)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var givenName = user.FindFirst(ClaimTypes.GivenName)?.Value ?? user.FindFirst("given_name")?.Value;
        var familyName = user.FindFirst(ClaimTypes.Surname)?.Value ?? user.FindFirst("family_name")?.Value;
        if (!string.IsNullOrWhiteSpace(givenName) && !string.IsNullOrWhiteSpace(familyName))
        {
            return $"{givenName} {familyName}";
        }

        if (!string.IsNullOrWhiteSpace(givenName))
        {
            return givenName;
        }

        return GetUserEmail();
    }

    public string GetUserEmail()
    {
        var user = GetPrincipal();
        if (user == null)
        {
            return string.Empty;
        }

        var email = EmailClaimTypes
            .Select(type => user.FindFirst(type)?.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return email ?? string.Empty;
    }

    public bool IsAuthenticated()
    {
        var context = _httpContextAccessor.HttpContext;
        return context?.User?.Identity?.IsAuthenticated ?? false;
    }

    private ClaimsPrincipal? GetPrincipal()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null || !(context.User?.Identity?.IsAuthenticated ?? false))
        {
            return null;
        }

        return context.User;
    }
}
