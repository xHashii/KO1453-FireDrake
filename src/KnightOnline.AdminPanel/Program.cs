using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using KnightOnline.AdminPanel.Components;
using KnightOnline.Database;
using KnightOnline.Database.Admin;
using KnightOnline.Database.Migrations;
using KnightOnline.Shared.Diagnostics;

// KnightOnline.AdminPanel - GM web panel (see docs/ADMIN_PANEL.md).
// Blazor (interactive server), single shared admin login from config.
// Binds to http://localhost:5100 by default: this is a root-level database
// tool, do not expose it publicly without a reverse proxy + real auth.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("GameDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:GameDatabase is missing.");

builder.Services.AddSingleton<ICharacterAdminRepository>(
    _ => new CharacterAdminRepository(connectionString));
builder.Services.AddSingleton(
    _ => new ItemSerialGenerator(builder.Configuration.GetValue("Admin:ServerNo", 1)));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ---- Auto-migration (same "no manual imports" rule as the game servers) ----
if (builder.Configuration.GetValue("Admin:AutoMigrate", true))
{
    await RunMigratorAsync(connectionString, "sql/kn_online");
}

async Task RunMigratorAsync(string connectionString, string scriptsFolder)
{
    try
    {
        var csb = new MySqlConnectionStringBuilder(connectionString);
        var settings = new DatabaseSettings(
            csb.Server, csb.Port, csb.Database, csb.UserID, csb.Password);
        // Under `dotnet run`, ContentRootPath is the project folder, but the
        // scripts are copied next to the executable - check both places.
        var fullPath = Path.Combine(app.Environment.ContentRootPath, scriptsFolder);
        if (!Directory.Exists(fullPath))
        {
            var nextToExe = Path.Combine(AppContext.BaseDirectory, scriptsFolder);
            if (Directory.Exists(nextToExe))
                fullPath = nextToExe;
        }
        await new DatabaseMigrator(settings, fullPath).MigrateAsync();
    }
    catch (Exception ex)
    {
        // The panel still starts (dashboard will show the DB as offline).
        KoLog.Error($"[admin] migration for {scriptsFolder} failed - panel starting anyway.", ex);
    }
}

// ---- Minimal config-password login (cookie auth, no database) ----
app.MapPost("/auth/login", async (HttpContext http,
    [FromForm] string username, [FromForm] string password) =>
{
    var expectedUser = app.Configuration["Admin:Username"] ?? "admin";
    var expectedPassword =
        Environment.GetEnvironmentVariable("KO_ADMIN_PASSWORD")
        ?? app.Configuration["Admin:Password"]
        ?? "";

    var ok = FixedTimeEquals(username, expectedUser) &
             FixedTimeEquals(password, expectedPassword) &
             expectedPassword.Length > 0;

    if (!ok)
        return Results.Redirect("/login?error=1");

    var identity = new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, "Admin") },
        CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    KoLog.Info($"[admin] {username} signed in from {http.Connection.RemoteIpAddress}");
    return Results.Redirect("/");
}).AllowAnonymous().DisableAntiforgery();

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).RequireAuthorization().DisableAntiforgery();

static bool FixedTimeEquals(string a, string b)
{
    var ha = SHA256.HashData(Encoding.UTF8.GetBytes(a));
    var hb = SHA256.HashData(Encoding.UTF8.GetBytes(b));
    return CryptographicOperations.FixedTimeEquals(ha, hb);
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

KoLog.Info("[admin] KnightOnline AdminPanel starting (default http://localhost:5100)");
app.Run();
