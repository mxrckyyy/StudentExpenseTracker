using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using StudentExpenseTracker.Data;
using StudentExpenseTracker.Models;
using StudentExpenseTracker.Services;
using StudentExpenseTracker.Components;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // TEMPORARY (debugging Render deploy crash): surface detailed Blazor
        // circuit errors in the browser so the real exception message and
        // stack trace are visible. Remove this after the crash is diagnosed.
        options.DetailedErrors = true;
    });

builder.Services.AddCascadingAuthenticationState();

// Register MySQL database context.
// The connection string can be provided either as a connection string
// (ConnectionStrings__DefaultConnection env var) or as a MySQL URL
// (DATABASE_URL env var, e.g. from Aiven), parsed below without losing
// special characters in the password.
var connectionString = ResolveConnectionString(builder.Configuration);
if (connectionString == null)
{
    Console.Error.WriteLine("[Startup] MySQL connection string missing. Set ConnectionStrings__DefaultConnection or DATABASE_URL.");
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: Array.Empty<int>())));

// Configure ASP.NET Core Identity with cookie authentication
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

// Register ExpenseService as Scoped (must match DbContext lifetime)
builder.Services.AddScoped<ExpenseService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // TEMPORARY (debugging Render deploy crash): render the full exception
    // details instead of the generic error page. Replace with
    // app.UseExceptionHandler("/Error") once the crash is diagnosed.
    app.UseDeveloperExceptionPage();
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

// Auth endpoints: Identity must run over a real HTTP request so the auth
// cookie can be written before the response starts (interactive Blazor
// Server circuits cannot set cookies, hence SignInManager must not be
// invoked from an interactive component).
app.MapPost("/account/login", async (
    HttpContext context,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["Username"].ToString().Trim();
    var password = form["Password"].ToString();

    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        return Results.Redirect("/login?error=" + Uri.EscapeDataString("Username and password are required."));

    var user = await userManager.FindByNameAsync(username);
    if (user == null)
        user = await userManager.FindByEmailAsync(username);

    if (user == null)
        return Results.Redirect("/login?error=" + Uri.EscapeDataString("Invalid username or email."));

    var result = await signInManager.PasswordSignInAsync(
        user.UserName!, password, isPersistent: true, lockoutOnFailure: false);

    return result.Succeeded
        ? Results.Redirect("/dashboard")
        : Results.Redirect("/login?error=" + Uri.EscapeDataString("Invalid password. Please try again."));
});

app.MapPost("/account/register", async (
    HttpContext context,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ExpenseService expenseService) =>
{
    var form = await context.Request.ReadFormAsync();
    var email = form["Email"].ToString().Trim();
    var password = form["Password"].ToString();
    var confirmPassword = form["ConfirmPassword"].ToString();

    if (password != confirmPassword)
        return Results.Redirect("/register?error=" + Uri.EscapeDataString("Passwords do not match."));

    if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
        return Results.Redirect("/register?error=" + Uri.EscapeDataString("Please enter a valid email address."));

    var user = new ApplicationUser { UserName = email, Email = email };
    var result = await userManager.CreateAsync(user, password);
    if (!result.Succeeded)
        return Results.Redirect("/register?error=" + Uri.EscapeDataString(string.Join(" ", result.Errors.Select(e => e.Description))));

    expenseService.SeedSampleData(user.Id);
    await signInManager.SignInAsync(user, isPersistent: true);
    return Results.Redirect("/dashboard");
});

app.MapPost("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

// Auto-apply pending EF migrations on startup (required on Render where
// the dotnet-ef CLI is not available). Creates the database and tables
// if they do not exist yet, and seeds the default categories.
// Using (scope) so nothing is held when startup ends.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.CanConnect())
    {
        db.Database.Migrate();
        app.Logger.LogInformation("Database connected and migrations applied successfully.");
    }
    else
    {
        app.Logger.LogError("Database connection failed. Verify the ConnectionStrings__DefaultConnection or DATABASE_URL environment variable in Render.");
    }
}

app.Run();

// Resolves the MySQL connection string from the application configuration.
// Priority: ConnectionStrings__DefaultConnection (connection-string format)
// -> DATABASE_URL (URL format, e.g. mysql://user:pass@host:port/db).
// Returns null when neither variable is set (the caller handles the fallback).
static string? ResolveConnectionString(ConfigurationManager configuration)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connectionString))
        return connectionString;

    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(databaseUrl))
        return null;

    return ParseDatabaseUrl(databaseUrl);
}

// Parses a MySQL URL such as mysql://avnadmin:p@ss%23word@host:port/db?ssl-mode=REQUIRED
// into a MySql connection string. Credentials are split manually (not via Uri.Query),
// so special characters in the password are preserved instead of being truncated.
// Returns null when the URL does not contain credentials (user:password@host).
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

    var mySqlBuilder = new MySqlConnectionStringBuilder
    {
        Server = hostPort,
        Database = Uri.UnescapeDataString(database),
        UserID = Uri.UnescapeDataString(user),
        Password = Uri.UnescapeDataString(password),
        SslMode = MySqlSslMode.Required,
        SslCa = "App_Data/certs/mysql-ca.pem"
    };

    return mySqlBuilder.ConnectionString;
}