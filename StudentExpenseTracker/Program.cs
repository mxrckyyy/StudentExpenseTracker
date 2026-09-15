using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using StudentExpenseTracker.Data;
using StudentExpenseTracker.Models;
using StudentExpenseTracker.Services;
using StudentExpenseTracker.Components;

var builder = WebApplication.CreateBuilder(args);

// Hosting platforms pass the HTTP port in the PORT environment variable
// (for example Render). Bind to it when present so traffic reaches the app.
var hostPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(hostPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{hostPort}");
}

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

// Local SQLite database. The .db file is self-contained inside the app folder,
// so the app never touches the shared MySQL server used by other projects.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "gasto-buster.db")}";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddScoped<ExpenseService>();

var app = builder.Build();

// Error visibility. Development and Staging always show the detailed developer
// exception page. Production stays locked down by default, but can opt in
// temporarily (for example while diagnosing a deployment) by setting
// DetailedErrors=true in the host environment.
var detailedErrors = app.Environment.IsDevelopment()
    || app.Environment.IsStaging()
    || app.Configuration.GetValue<bool>("DetailedErrors");

if (detailedErrors)
{
    app.UseDeveloperExceptionPage();
}
else
{
    // The built-in handler logs the full exception (message + stack trace)
    // before re-executing the /Error page.
    app.UseExceptionHandler("/Error");
}

if (!app.Environment.IsDevelopment())
{
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
    if (user == null)
        return Results.Redirect("/register?error=" + Uri.EscapeDataString("We couldn't create your account right now. Please try again in a moment."));

    expenseService.SeedSampleData(user.Id);

    await SignInAsync(context, user);
    return Results.Redirect("/dashboard");
});

app.MapPost("/account/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

// Startup migration: apply the SQLite schema BEFORE the app serves any request
// so no component or page can query a missing table. The .db file is created
// automatically inside the app folder when the first migration runs.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        dbContext.Database.Migrate();
        app.Logger.LogInformation("SQLite database ready at: {Source}", dbContext.Database.GetDbConnection().DataSource);
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "SQLite database migration failed during startup.");
        throw;
    }
}

app.Run();

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