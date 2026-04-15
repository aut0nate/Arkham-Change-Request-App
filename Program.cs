using Azure.Identity;
using Azure.Storage.Blobs;
using ArkhamChangeRequest.Data;
using ArkhamChangeRequest.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

var keyVaultUrl = builder.Configuration["KeyVaultSettings:VaultUrl"];
if (!string.IsNullOrWhiteSpace(keyVaultUrl))
{
    var credential = new DefaultAzureCredential();
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), credential);
}

var tenantId = builder.Configuration["AzureAd:TenantId"];
var clientId = builder.Configuration["AzureAd:ClientId"];
var clientSecret = builder.Configuration["AzureAd:ClientSecret"] ?? builder.Configuration["AZUREAD_CLIENT_SECRET"];
var publicOrigin = builder.Configuration["App:PublicOrigin"];
var callbackPath = builder.Configuration["AzureAd:CallbackPath"] ?? "/signin-oidc";
var signedOutCallbackPath = builder.Configuration["AzureAd:SignedOutCallbackPath"] ?? "/signout-callback-oidc";

if (string.IsNullOrWhiteSpace(tenantId))
{
    throw new InvalidOperationException("AzureAd:TenantId is not configured.");
}

if (string.IsNullOrWhiteSpace(clientId))
{
    throw new InvalidOperationException("AzureAd:ClientId is not configured.");
}

if (string.IsNullOrWhiteSpace(clientSecret))
{
    throw new InvalidOperationException("AzureAd client secret is not configured. Set AzureAd:ClientSecret or AZUREAD_CLIENT_SECRET.");
}

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        options.ClientSecret = clientSecret;
        options.SaveTokens = true;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.FormPost;
        options.UsePkce = true;
        options.Events ??= new OpenIdConnectEvents();
        options.Events.OnRedirectToIdentityProvider = context =>
        {
            if (!string.IsNullOrWhiteSpace(publicOrigin))
            {
                context.ProtocolMessage.RedirectUri = BuildAbsoluteUri(publicOrigin, callbackPath);
            }

            return Task.CompletedTask;
        };
        options.Events.OnRedirectToIdentityProviderForSignOut = context =>
        {
            if (!string.IsNullOrWhiteSpace(publicOrigin))
            {
                context.ProtocolMessage.PostLogoutRedirectUri = BuildAbsoluteUri(publicOrigin, signedOutCallbackPath);
            }

            return Task.CompletedTask;
        };
        options.Events.OnTokenValidated = context =>
        {
            if (context.Principal?.Identity is ClaimsIdentity identity)
            {
                var displayName = ResolveDisplayName(identity);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    ReplaceClaim(identity, ClaimTypes.Name, displayName);
                    ReplaceClaim(identity, "name", displayName);
                }

                var emailAddress = ResolveEmailAddress(identity);
                if (!string.IsNullOrWhiteSpace(emailAddress))
                {
                    ReplaceClaim(identity, ClaimTypes.Email, emailAddress);
                    ReplaceClaim(identity, "email", emailAddress);
                }
            }

            return Task.CompletedTask;
        };
    },
    cookieOptions =>
    {
        cookieOptions.LoginPath = "/Account/Login";
        cookieOptions.LogoutPath = "/Account/Logout";
        cookieOptions.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ArkhamAuthorized", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("ChangeApproversOnly", policy => policy.RequireAuthenticatedUser());
    options.FallbackPolicy = options.GetPolicy("ArkhamAuthorized");
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter("ArkhamAuthorized"));
});
builder.Services.AddRazorPages();

var dataProtectionPath = builder.Configuration["DataProtection:Path"];
if (!string.IsNullOrWhiteSpace(dataProtectionPath))
{
    var fullDataProtectionPath = Path.IsPathRooted(dataProtectionPath)
        ? dataProtectionPath
        : Path.Combine(Directory.GetCurrentDirectory(), dataProtectionPath);

    Directory.CreateDirectory(fullDataProtectionPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(fullDataProtectionPath));
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsightsConnectionString"]
        ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var configuredProvider = builder.Configuration["Database:Provider"];
    var sqlServerConnectionString = builder.Configuration["SqlConnectionString"]
        ?? builder.Configuration.GetConnectionString("DefaultConnection");
    var sqliteConnectionString = builder.Configuration.GetConnectionString("Sqlite");
    var provider = ResolveDatabaseProvider(configuredProvider, sqlServerConnectionString, sqliteConnectionString);

    if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var configuredSqliteConnectionString = string.IsNullOrWhiteSpace(sqliteConnectionString)
            ? "Data Source=arkham-change.db"
            : sqliteConnectionString;

        var resolvedSqliteConnectionString = NormalizeSqliteConnectionString(configuredSqliteConnectionString);
        EnsureSqliteDirectoryExists(resolvedSqliteConnectionString);
        options.UseSqlite(resolvedSqliteConnectionString);
        return;
    }

    if (string.IsNullOrWhiteSpace(sqlServerConnectionString))
    {
        throw new InvalidOperationException("No SQL Server connection string is configured.");
    }

    options.UseSqlServer(sqlServerConnectionString);
});

var storageProvider = builder.Configuration["Storage:Provider"];
if (string.Equals(storageProvider, "LocalFiles", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IBlobStorageService, LocalFileStorageService>();
}
else
{
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

        var storageConnectionString = configuration["AzureStorageConnectionString"]
            ?? configuration.GetConnectionString("AzureStorage");

        if (string.IsNullOrWhiteSpace(storageConnectionString))
        {
            throw new InvalidOperationException("Azure Storage configuration is missing.");
        }

        return new BlobServiceClient(storageConnectionString);
    });

    builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
}

builder.Services.AddScoped<IChangeRequestService, ChangeRequestService>();
builder.Services.AddScoped<IUserService, ClaimsUserService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while creating the database.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

var disableHttpsRedirection = builder.Configuration.GetValue<bool>("App:DisableHttpsRedirection");

app.UseForwardedHeaders();
if (!disableHttpsRedirection)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ChangeRequest}/{action=Create}/{id?}");
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

static string? ResolveDisplayName(ClaimsIdentity identity)
{
    var claimValues = new[]
    {
        identity.FindFirst("name")?.Value,
        identity.FindFirst(ClaimTypes.Name)?.Value,
        identity.FindFirst("preferred_username")?.Value,
        identity.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value,
        identity.FindFirst("http://schemas.microsoft.com/identity/claims/displayname")?.Value,
        identity.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/displayname")?.Value
    };

    var resolved = claimValues.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    if (!string.IsNullOrWhiteSpace(resolved))
    {
        return resolved;
    }

    var givenName = identity.FindFirst(ClaimTypes.GivenName)?.Value ?? identity.FindFirst("given_name")?.Value;
    var familyName = identity.FindFirst(ClaimTypes.Surname)?.Value ?? identity.FindFirst("family_name")?.Value;

    if (!string.IsNullOrWhiteSpace(givenName) && !string.IsNullOrWhiteSpace(familyName))
    {
        return $"{givenName} {familyName}".Trim();
    }

    return givenName ?? familyName;
}

static string? ResolveEmailAddress(ClaimsIdentity identity)
{
    var claimValues = new[]
    {
        identity.FindFirst(ClaimTypes.Email)?.Value,
        identity.FindFirst("email")?.Value,
        identity.FindFirst("preferred_username")?.Value,
        identity.FindFirst(ClaimTypes.Upn)?.Value,
        identity.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
    };

    return claimValues.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

static void ReplaceClaim(ClaimsIdentity identity, string claimType, string value)
{
    var existingClaims = identity.FindAll(claimType).ToList();
    foreach (var claim in existingClaims)
    {
        identity.TryRemoveClaim(claim);
    }

    identity.AddClaim(new Claim(claimType, value));
}

static string BuildAbsoluteUri(string origin, string path)
{
    var normalizedOrigin = origin.Trim().TrimEnd('/');
    var normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
    if (!normalizedPath.StartsWith('/'))
    {
        normalizedPath = "/" + normalizedPath;
    }

    return normalizedOrigin + normalizedPath;
}

static string ResolveDatabaseProvider(string? configuredProvider, string? sqlServerConnectionString, string? sqliteConnectionString)
{
    if (!string.IsNullOrWhiteSpace(configuredProvider))
    {
        return configuredProvider;
    }

    if (!string.IsNullOrWhiteSpace(sqliteConnectionString) && string.IsNullOrWhiteSpace(sqlServerConnectionString))
    {
        return "Sqlite";
    }

    return "SqlServer";
}

static void EnsureSqliteDirectoryExists(string connectionString)
{
    const string prefix = "Data Source=";
    if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    var dataSource = connectionString[prefix.Length..].Trim().Trim('"');
    if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
    {
        return;
    }

    var fullPath = Path.IsPathRooted(dataSource)
        ? dataSource
        : Path.Combine(AppContext.BaseDirectory, dataSource);

    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }
}

static string NormalizeSqliteConnectionString(string connectionString)
{
    const string prefix = "Data Source=";
    if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return connectionString;
    }

    var dataSource = connectionString[prefix.Length..].Trim().Trim('"');
    if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:" || Path.IsPathRooted(dataSource))
    {
        return connectionString;
    }

    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), dataSource);
    return $"{prefix}{fullPath}";
}
