using System;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArkhamChangeRequest.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IConfiguration _configuration;

    public AccountController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool forceNewSession = false)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = Url.Action("Create", "ChangeRequest") ?? "/";
        }

        var authenticationProperties = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };

        if (forceNewSession)
        {
            authenticationProperties.SetParameter("prompt", "login");
            authenticationProperties.SetParameter("max_age", "0");
        }

        return Challenge(authenticationProperties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        var callbackUrl = Url.Action(nameof(SignedOut), "Account", null, Request.Scheme)
                         ?? _configuration["Auth0:LogoutUrl"]
                         ?? "/";

        var authenticationProperties = new AuthenticationProperties
        {
            RedirectUri = callbackUrl
        };

        return SignOut(authenticationProperties,
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public IActionResult SignedOut()
    {
        ViewBag.LoginUrl = Url.Action(nameof(Login), "Account", new { forceNewSession = true });
        return View();
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        var allowedGroups = (_configuration.GetSection("Auth0:AllowedGroups").Get<string[]>() ?? Array.Empty<string>())
            .Select(g => g?.Trim())
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        ViewBag.AllowedGroups = allowedGroups;

        var groupClaimTypes = _configuration.GetSection("Auth0:GroupClaimTypes").Get<string[]>() ?? Array.Empty<string>();
        var userGroupClaims = User?.Claims?
            .Where(c => groupClaimTypes.Any(type => string.Equals(type, c.Type, StringComparison.OrdinalIgnoreCase)))
            .Select(c => $"{c.Type}: {c.Value}")
            .ToArray() ?? Array.Empty<string>();
        ViewBag.UserGroupClaims = userGroupClaims;

        return View();
    }
}
