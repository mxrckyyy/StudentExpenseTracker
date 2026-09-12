using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Security.Claims;
using StudentExpenseTracker.Data;
using StudentExpenseTracker.Models;
using StudentExpenseTracker.Services;
using StudentExpenseTracker.Components;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

// Cookie-based authentication backed by the Users table (no Identity UI).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });
builder.Services.AddAuthorization();

// Resolve the MySQL connection string. Priority:
//   ConnectionStrings__DefaultConnection (Render env var / appsettings.json)
//   -> DATABASE_URL (URL format, e.g. mysql://avnadmin:pass@host:port/db)
// The password's special characters are unescaped so credentials pass cleanly.
var resolvedConnectionString = ResolveConnectionString(builder.Configuration, out var connectionSource);
var connectionString = string.IsNullOrWhiteSpace(resolvedConnectionString)
    ? (builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty)
    : resolvedConnectionString;

// Aiven only accepts encrypted connections; force SslMode=Required so the
// deployed app always talks TLS regardless of how the env var was written.
var securedConnectionString = EnforceSslMode(connectionString, builder.Environment, out var sslModeLabel);
Console.WriteLine($"[Startup] MySQL target -> {connectionSource}; SslMode: {sslModeLabel}");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(securedConnectionString, new MySqlServerVersion(new Version(8, 0, 36)),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

builder.Services.AddScoped<ExpenseService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    // Only redirect HTTPS locally during development on your PC
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Auth endpoints: set the auth cookie over a real HTTP request (interactive
// Blazor Server circuits cannot set cookies).
app.MapPost("/account/login", async (HttpContext context, ExpenseService expenseService) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["Username"].ToString().Trim();
    var password = form["Password"].ToString();

    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        return Results.Redirect("/login?error=" + Uri.EscapeDataString("Username and password are required."));

    var user = expenseService.FindByUsernameOrEmail(username);
    if (user == null)
        return Results.Redirect("/login?error=" + Uri.EscapeDataString("Invalid username or email."));

    var hasher = new PasswordHasher<AppUser>();
    var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
    if (verification == PasswordVerificationResult.Failed)
        return Results.Redirect("/login?error=" + Uri.EscapeDataString("Invalid password. Please try again."));

    await SignInAsync(context, user);
    return Results.Redirect("/dashboard");
});

app.MapPost("/account/register", async (HttpContext context, ExpenseService expenseService) =>
{
    var form = await context.Request.ReadFormAsync();
    var email = form["Email"].ToString().Trim();
    var password = form["Password"].ToString();
    var confirmPassword = form["ConfirmPassword"].ToString();

    if (password != confirmPassword)
        return Results.Redirect("/register?error=" + Uri.EscapeDataString("Passwords do not match."));

    if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
        return Results.Redirect("/register?error=" + Uri.EscapeDataString("Please enter a valid email address."));

    if (expenseService.FindByUsernameOrEmail(email) != null)
        return Results.Redirect("/register?error=" + Uri.EscapeDataString("An account with that email already exists."));

    var hasher = new PasswordHasher<AppUser>();
    var passwordHash = hasher.HashPassword(new AppUser { UserName = email, Email = email }, password);
    var user = expenseService.CreateUser(email, passwordHash);
    expenseService.SeedSampleData(user.Id);

    await SignInAsync(context, user);
    return Results.Redirect("/dashboard");
});

app.MapPost("/account/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

// Safe startup migration: connect-check first so a failed database connection
// (wrong credentials, unprovisioned host, firewall) logs a clear message
// instead of crashing the container process on Render.
// Migrate() creates and applies every EF table (Users, Expenses, Categories,
// Budgets) and inserts the seeded default categories from OnModelCreating.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbContext.Database.CanConnect())
    {
        dbContext.Database.Migrate();
        app.Logger.LogInformation("Database connected and migrations applied successfully.");
    }
    else
    {
        app.Logger.LogError("Database connection failed. Verify the ConnectionStrings__DefaultConnection or DATABASE_URL environment variable in Render.");
    }
}

app.Run();

// Resolves the MySQL connection string from the application configuration.
// Priority: ConnectionStrings__DefaultConnection ->
// DATABASE_URL (URL format). Returns null when neither variable is set so the
// caller can fall back to the appsettings.json connection string.
static string? ResolveConnectionString(ConfigurationManager configuration, out string source)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        source = "ConnectionStrings__DefaultConnection (env var overrides appsettings.json)";
        return connectionString;
    }

    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        source = "DATABASE_URL env var";
        return ParseDatabaseUrl(databaseUrl);
    }

    source = "appsettings.json DefaultConnection fallback";
    return null;
}

// Parses a MySQL URL such as mysql://avnadmin:p@ss%23word@host:port/db
// into a MySql connection string. Credentials are split manually so special
// characters in the password are preserved, then unescaped so they pass
// cleanly without truncation or syntax errors. SslMode is forced to Required
// because Aiven refuses plaintext connections.
static string? ParseDatabaseUrl(string databaseUrl)
{
    var rest = databaseUrl;
    var schemeEnd = databaseUrl.IndexOf("://", StringComparison.Ordinal);
    if (schemeEnd >= 0)
        rest = databaseUrl[(schemeEnd + 3)..];

    var atIndex = rest.LastIndexOf('@');
    if (atIndex < 0)
        return null;

    var credentials = rest[..atIndex];
    var hostAndDb = rest[(atIndex + 1)..];

    var queryIndex = hostAndDb.IndexOf('?');
    if (queryIndex >= 0)
        hostAndDb = hostAndDb[..queryIndex];

    var slashIndex = hostAndDb.IndexOf('/');
    var hostPort = slashIndex < 0 ? hostAndDb : hostAndDb[..slashIndex];
    var database = slashIndex < 0 ? string.Empty : hostAndDb[(slashIndex + 1)..];

    var colonIndex = credentials.IndexOf(':');
    var user = colonIndex < 0 ? credentials : credentials[..colonIndex];
    var password = colonIndex < 0 ? string.Empty : credentials[(colonIndex + 1)..];

    var sqlBuilder = new MySqlConnectionStringBuilder
    {
        Server = hostPort,
        Database = Uri.UnescapeDataString(database),
        UserID = Uri.UnescapeDataString(user),
        Password = Uri.UnescapeDataString(password),
        SslMode = MySqlSslMode.Required
    };

    return sqlBuilder.ConnectionString;
}

// Rewrites the resolved connection string so SslMode is always Required for
// the deployed (non-Development) app. Local Development keeps whatever SSL
// mode was configured so a local XAMPP instance can run without TLS.
static string EnforceSslMode(string connectionString, IHostEnvironment environment, out string sslModeLabel)
{
    var sqlBuilder = new MySqlConnectionStringBuilder(connectionString);
    if (!environment.IsDevelopment())
    {
        sqlBuilder.SslMode = MySqlSslMode.Required;
        sslModeLabel = "Required";
    }
    else
    {
        sslModeLabel = sqlBuilder.SslMode.ToString();
    }
    return sqlBuilder.ConnectionString;
}

// Builds the authenticated principal for a user and writes the auth cookie.
// The cookie carries the user id (NameIdentifier), name, and email claims so
// the interactive components and [Authorize]-guarded pages can resolve the
// current user without re-querying the database on every render.
static async Task SignInAsync(HttpContext context, AppUser user)
{
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Name, user.Email ?? user.UserName ?? user.Id),
        new Claim(ClaimTypes.Email, user.Email ?? string.Empty)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties { IsPersistent = true });
}