using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArkhamChangeRequest.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
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
        var callbackUrl = Url.Action(nameof(SignedOut), "Account", null, Request.Scheme) ?? "/";

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
        return View();
    }
}
