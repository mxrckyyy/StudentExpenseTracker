using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentExpenseTracker.Data;
using StudentExpenseTracker.Models;
using StudentExpenseTracker.Services;
using StudentExpenseTracker.Components;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

// Register MySQL database context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

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
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();