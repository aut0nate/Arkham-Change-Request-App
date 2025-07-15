using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using ArkhamChangeRequest.Data;
using ArkhamChangeRequest.Services;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Note: Authentication is handled by Azure App Service Easy Auth
// No need to configure Microsoft.Identity.Web when using Easy Auth

// Add Microsoft Identity (Azure AD) authentication - DISABLED when using Easy Auth
/*
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
*/

// Configure controllers - removed authentication requirement when using Easy Auth
builder.Services.AddControllersWithViews();
/* Original authentication code:
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});
*/

// Add UI controllers - DISABLED when using Easy Auth
/*
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();
*/
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
builder.Services.AddSingleton(x => 
{
    // Try Key Vault first, then fallback to connection string
    var storageConnectionString = builder.Configuration["AzureStorageConnectionString"] 
        ?? builder.Configuration.GetConnectionString("AzureStorage");
    return new BlobServiceClient(storageConnectionString);
});

// Add custom services
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<IChangeRequestService, ChangeRequestService>();
builder.Services.AddScoped<IUserService, EntraIdUserService>();
builder.Services.AddHttpContextAccessor();

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

// Authentication handled by Azure App Service Easy Auth
// app.UseAuthentication();
// app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ChangeRequest}/{action=Create}/{id?}");
app.MapRazorPages();

app.Run();
