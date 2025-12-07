using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs;
using ArkhamChangeRequest.Data;
using ArkhamChangeRequest.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

var keyVaultUrl = builder.Configuration["KeyVaultSettings:VaultUrl"];
if (!string.IsNullOrWhiteSpace(keyVaultUrl))
{
    var credential = new DefaultAzureCredential();
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), credential);
}

var auth0Section = builder.Configuration.GetSection("Auth0");
var auth0Domain = auth0Section["Domain"] ?? throw new InvalidOperationException("Auth0:Domain is not configured.");
var auth0ClientId = auth0Section["ClientId"] ?? throw new InvalidOperationException("Auth0:ClientId is not configured.");
var auth0ClientSecret = auth0Section["ClientSecret"];
auth0ClientSecret ??= builder.Configuration["AUTH0_CLIENT_SECRET"];
auth0ClientSecret ??= builder.Configuration["SESSION_SECRET"];

if (string.IsNullOrWhiteSpace(auth0ClientSecret))
{
    throw new InvalidOperationException("Auth0 client secret is not configured. Set Auth0:ClientSecret or AUTH0_CLIENT_SECRET.");
}

var auth0CallbackPath = auth0Section["CallbackPath"] ?? "/callback";
var auth0LogoutUrl = auth0Section["LogoutUrl"];
var auth0Connection = auth0Section["Connection"];
var allowedGroups = NormalizeList(auth0Section.GetSection("AllowedGroups").Get<string[]>() ?? Array.Empty<string>());
var allowedGroupIds = NormalizeList(auth0Section.GetSection("AllowedGroupIds").Get<string[]>() ?? Array.Empty<string>());
var allowedEmails = NormalizeList(auth0Section.GetSection("AllowedEmails").Get<string[]>() ?? Array.Empty<string>());
var groupClaimTypes = NormalizeList(auth0Section.GetSection("GroupClaimTypes").Get<string[]>() ?? new[] { "groups" });
var changeApproverGroups = NormalizeList(auth0Section.GetSection("ApproverGroups").Get<string[]>() ?? Array.Empty<string>());
var changeApproverGroupIds = NormalizeList(auth0Section.GetSection("ApproverGroupIds").Get<string[]>() ?? Array.Empty<string>());
var preferredNameClaimTypes = NormalizeList(auth0Section.GetSection("PreferredNameClaimTypes").Get<string[]>() ?? Array.Empty<string>());
var allowedGroupNamesSet = new HashSet<string>(allowedGroups, StringComparer.OrdinalIgnoreCase);
var allowedGroupIdsSet = new HashSet<string>(
    allowedGroupIds.Select(NormalizeGuidString),
    StringComparer.OrdinalIgnoreCase);
var allowedEmailSet = new HashSet<string>(allowedEmails, StringComparer.OrdinalIgnoreCase);
var changeApproverGroupSet = new HashSet<string>(changeApproverGroups, StringComparer.OrdinalIgnoreCase);
var changeApproverGroupIdsSet = new HashSet<string>(
    changeApproverGroupIds.Select(NormalizeGuidString),
    StringComparer.OrdinalIgnoreCase);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
})
.AddOpenIdConnect(options =>
{
    options.Authority = $"https://{auth0Domain}";
    options.ClientId = auth0ClientId;
    options.ClientSecret = auth0ClientSecret;
    options.CallbackPath = auth0CallbackPath;
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.SaveTokens = true;
    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
    options.ClaimActions.MapJsonKey("name", "name");
    options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");
    options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "family_name");
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };

    options.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProvider = context =>
        {
            if (!string.IsNullOrWhiteSpace(auth0Connection))
            {
                context.ProtocolMessage.SetParameter("connection", auth0Connection);
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            if (context.Principal?.Identity is ClaimsIdentity identity)
            {
                var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Auth0Claims");

                foreach (var claimType in groupClaimTypes)
                {
                    var matchingClaims = identity.FindAll(claimType).ToList();
                    foreach (var groupClaim in matchingClaims)
                    {
                        if (!identity.HasClaim(ClaimTypes.Role, groupClaim.Value))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, groupClaim.Value));
                        }
                    }

                    if (matchingClaims.Any())
                    {
                        logger.LogInformation("Received group claim type {ClaimType} with values: {Values}",
                            claimType,
                            string.Join(", ", matchingClaims.Select(c => c.Value)));
                    }
                }

                var emailClaim = GetUserEmailClaimFromIdentity(identity);
                if (!string.IsNullOrWhiteSpace(emailClaim))
                {
                    logger.LogInformation("User email resolved to {Email}", emailClaim);
                }

            }

            return Task.CompletedTask;
        },
        OnTicketReceived = async context =>
        {
            if (context.Principal?.Identity is ClaimsIdentity identity)
            {
                var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Auth0Claims");

                var displayName = ResolveDisplayName(identity, preferredNameClaimTypes);

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    var accessToken = context.Properties?.GetTokenValue("access_token");
                    displayName = await FetchDisplayNameFromUserInfoAsync(
                        context.HttpContext.RequestServices,
                        auth0Domain,
                        accessToken,
                        logger);
                }

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    EnsureClaimValue(identity, ClaimTypes.Name, displayName);
                    EnsureClaimValue(identity, "name", displayName);
                    logger.LogInformation("Final display name set to {DisplayName}", displayName);
                }
                else
                {
                    var claimSummary = string.Join(", ",
                        identity.Claims.Select(c => $"{c.Type}:{c.Value}"));
                    logger.LogWarning("Unable to resolve display name. Claims: {Claims}", claimSummary);
                }
            }

            return;
        },
        OnRedirectToIdentityProviderForSignOut = context =>
        {
            var logoutUri = $"https://{auth0Domain}/v2/logout?client_id={auth0ClientId}";
            var postLogoutUri = context.Properties?.RedirectUri ?? auth0LogoutUrl ?? $"{context.Request.Scheme}://{context.Request.Host}";
            if (!string.IsNullOrWhiteSpace(postLogoutUri))
            {
                logoutUri += $"&returnTo={Uri.EscapeDataString(postLogoutUri)}";
            }

            context.Response.Redirect(logoutUri);
            context.HandleResponse();
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ArkhamAuthorized", policy =>
    {
        policy.RequireAuthenticatedUser();
        if (allowedGroupNamesSet.Any() || allowedGroupIdsSet.Any())
        {
            policy.RequireAssertion(context =>
            {
                if (context.User == null)
                {
                    return false;
                }

                var claimValues = groupClaimTypes
                    .SelectMany(type => context.User.FindAll(type))
                    .Select(claim => claim.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList();

        if (allowedGroupIdsSet.Any())
        {
            var hasAllowedId = claimValues.Any(value =>
                ExpandGroupTokens(value).Any(token =>
                    allowedGroupIdsSet.Contains(NormalizeGuidString(token))));

            if (hasAllowedId)
            {
                return true;
            }
        }

        if (allowedGroupNamesSet.Any())
        {
            var hasAllowedName = claimValues.Any(value =>
                ExpandGroupTokens(value).Any(token => allowedGroupNamesSet.Contains(token)));

            if (hasAllowedName)
            {
                return true;
            }
        }

                if (allowedEmailSet.Any())
                {
                    var email = GetUserEmailClaim(context.User);
                    if (!string.IsNullOrWhiteSpace(email) && allowedEmailSet.Contains(email))
                    {
                        return true;
                    }
                }

        return false;
    });
        }
    });

    options.AddPolicy("ChangeApproversOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            if (!changeApproverGroupSet.Any() && !changeApproverGroupIdsSet.Any())
            {
                return false;
            }

            return UserMatchesConfiguredGroups(
                context.User,
                changeApproverGroupSet,
                changeApproverGroupIdsSet,
                groupClaimTypes);
        });
    });
    options.FallbackPolicy = options.GetPolicy("ArkhamAuthorized");
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter("ArkhamAuthorized"));
});
builder.Services.AddRazorPages();

// Add Application Insights telemetry
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    // Try Key Vault first, then fallback to app settings
    options.ConnectionString = builder.Configuration["ApplicationInsightsConnectionString"] 
        ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
});

// Add Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // Try Key Vault first, then fallback to connection string
    var connectionString = builder.Configuration["SqlConnectionString"] 
        ?? builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});

// Add Azure Blob Storage
builder.Services.AddSingleton(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var accountUrl = configuration["BlobSettings:AccountUrl"];
    var accountName = configuration["BlobSettings:AccountName"];
    var credential = new DefaultAzureCredential();

    if (!string.IsNullOrWhiteSpace(accountUrl))
    {
        return new BlobServiceClient(new Uri(accountUrl), credential);
    }

    if (!string.IsNullOrWhiteSpace(accountName))
    {
        var uri = new Uri($"https://{accountName}.blob.core.windows.net");
        return new BlobServiceClient(uri, credential);
    }

    // Try Key Vault first, then fallback to connection string
    var storageConnectionString = configuration["AzureStorageConnectionString"]
        ?? configuration.GetConnectionString("AzureStorage");

    if (string.IsNullOrWhiteSpace(storageConnectionString))
    {
        throw new InvalidOperationException("Azure Storage configuration is missing.");
    }

    return new BlobServiceClient(storageConnectionString);
});

// Add custom services
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<IChangeRequestService, ChangeRequestService>();
builder.Services.AddScoped<IUserService, ClaimsUserService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

var app = builder.Build();

// Automatically apply database migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
        // You could also use: dbContext.Database.Migrate(); if you have migrations
    }
    catch (Exception ex)
    {
        // Log the exception but don't crash the app
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while creating the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ChangeRequest}/{action=Create}/{id?}");
app.MapRazorPages();

app.Run();

static string? ResolveDisplayName(ClaimsIdentity identity, IReadOnlyCollection<string> preferredClaimTypes)
{
    if (preferredClaimTypes != null)
    {
        foreach (var claimType in preferredClaimTypes)
        {
            var value = identity.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
    }

    var fallbackValues = new[]
    {
        identity.FindFirst("name")?.Value,
        identity.FindFirst("http://schemas.microsoft.com/identity/claims/displayname")?.Value,
        identity.FindFirst("displayName")?.Value,
        identity.FindFirst("full_name")?.Value,
        identity.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/displayname")?.Value,
        identity.FindFirst(ClaimTypes.Name)?.Value
    };

    var resolved = fallbackValues.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    if (!string.IsNullOrWhiteSpace(resolved))
    {
        return resolved;
    }

    var given = identity.FindFirst(ClaimTypes.GivenName)?.Value ?? identity.FindFirst("given_name")?.Value;
    var family = identity.FindFirst(ClaimTypes.Surname)?.Value ?? identity.FindFirst("family_name")?.Value;

    if (!string.IsNullOrWhiteSpace(given) && !string.IsNullOrWhiteSpace(family))
    {
        return $"{given} {family}".Trim();
    }

    if (!string.IsNullOrWhiteSpace(given))
    {
        return given;
    }

    if (!string.IsNullOrWhiteSpace(family))
    {
        return family;
    }

    return null;
}

static bool UserMatchesConfiguredGroups(
    ClaimsPrincipal? user,
    HashSet<string> groupNames,
    HashSet<string> groupIds,
    IEnumerable<string> groupClaimTypes)
{
    if (user == null)
    {
        return false;
    }

    bool Matches(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var trimmed = rawValue.Trim();
        if (groupNames.Contains(trimmed))
        {
            return true;
        }

        var normalized = NormalizeGuidString(trimmed);
        if (!string.IsNullOrWhiteSpace(normalized) && groupIds.Contains(normalized))
        {
            return true;
        }

        foreach (var token in ExpandGroupTokens(trimmed))
        {
            if (groupNames.Contains(token))
            {
                return true;
            }

            var normalizedToken = NormalizeGuidString(token);
            if (!string.IsNullOrWhiteSpace(normalizedToken) && groupIds.Contains(normalizedToken))
            {
                return true;
            }
        }

        return false;
    }

    foreach (var roleClaim in user.FindAll(ClaimTypes.Role))
    {
        if (Matches(roleClaim.Value))
        {
            return true;
        }
    }

    foreach (var claimType in groupClaimTypes ?? Array.Empty<string>())
    {
        foreach (var groupClaim in user.FindAll(claimType))
        {
            if (Matches(groupClaim.Value))
            {
                return true;
            }
        }
    }

    return false;
}

static void EnsureClaimValue(ClaimsIdentity identity, string claimType, string value)
{
    if (identity == null || string.IsNullOrWhiteSpace(claimType) || string.IsNullOrWhiteSpace(value))
    {
        return;
    }

    var existingClaims = identity.FindAll(claimType).ToList();
    foreach (var claim in existingClaims)
    {
        identity.TryRemoveClaim(claim);
    }

    identity.AddClaim(new Claim(claimType, value));
}

static async Task<string?> FetchDisplayNameFromUserInfoAsync(IServiceProvider services, string auth0Domain, string? accessToken, ILogger logger)
{
    if (string.IsNullOrWhiteSpace(accessToken))
    {
        logger.LogWarning("Cannot call Auth0 userinfo without an access token.");
        return null;
    }

    var clientFactory = services.GetRequiredService<IHttpClientFactory>();
    var client = clientFactory.CreateClient();

    using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{auth0Domain}/userinfo");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    try
    {
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Auth0 userinfo call failed with status {StatusCode}", response.StatusCode);
            return null;
        }

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(contentStream);
        var root = document.RootElement;

        var name = TryGetJsonString(root, "name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var given = TryGetJsonString(root, "given_name");
        var family = TryGetJsonString(root, "family_name");

        if (!string.IsNullOrWhiteSpace(given) && !string.IsNullOrWhiteSpace(family))
        {
            return $"{given} {family}";
        }

        return given ?? family ?? TryGetJsonString(root, "nickname");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error while retrieving Auth0 userinfo profile.");
        return null;
    }
}

static string? TryGetJsonString(JsonElement element, string propertyName)
{
    return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
        ? prop.GetString()
        : null;
}

static string[] NormalizeList(IEnumerable<string>? values)
{
    return values?
        .Select(v => v?.Trim())
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? Array.Empty<string>();
}

static string NormalizeGuidString(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return string.Empty;
    }

    var trimmed = value.Trim();
    return Guid.TryParse(trimmed, out var guid)
        ? guid.ToString("D")
        : trimmed;
}

static IEnumerable<string> ExpandGroupTokens(string? rawValue)
{
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        yield break;
    }

    var trimmed = rawValue.Trim();
    yield return trimmed;

    char[] separators = { '#', '/', '|', ',', ';', ':' };
    var segments = trimmed.Split(separators, StringSplitOptions.RemoveEmptyEntries);
    foreach (var segment in segments)
    {
        var token = segment.Trim();
        if (!string.IsNullOrWhiteSpace(token))
        {
            yield return token;
        }
    }
}

static string? GetUserEmailClaim(ClaimsPrincipal? user)
{
    var email = user?.FindFirst(ClaimTypes.Email)?.Value
        ?? user?.FindFirst("preferred_username")?.Value
        ?? user?.FindFirst(ClaimTypes.Upn)?.Value;

    return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
}

static string? GetUserEmailClaimFromIdentity(ClaimsIdentity? identity)
{
    return identity == null ? null : GetUserEmailClaim(new ClaimsPrincipal(identity));
}
