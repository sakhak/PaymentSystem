using Microsoft.EntityFrameworkCore;
using PaymentSystem.Data;
using PaymentSystem.Services;
using PaymentSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
// Console logging also works under restricted Windows development accounts.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.

builder.Services.AddControllers(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddRazorPages(options => options.Conventions.AuthorizeFolder("/Admin", "AdminOnly"));
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "PaymentSystem.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnValidatePrincipal = async context =>
    {
        var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.GetUserAsync(context.Principal!);
        if (user is null || !user.IsActive ||
            context.Principal!.FindFirstValue("AspNet.Identity.SecurityStamp") != await manager.GetSecurityStampAsync(user))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});
builder.Services.AddScoped<PaymentService>();
builder.Services.AddHostedService<PaymentVerificationWorker>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddScoped<KhqrService>();
builder.Services.AddHttpClient<BakongService>(
    (serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var baseUrl = config["Bakong:BaseUrl"] ?? throw new InvalidOperationException("Bakong Url is missing.");
            var address = new Uri(baseUrl.TrimEnd('/') + "/");
            var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
            if (address.Scheme != Uri.UriSchemeHttps && !(environment.IsDevelopment() && address.IsLoopback))
                throw new InvalidOperationException("Bakong requires HTTPS. HTTP is only allowed for local development test servers.");
            client.BaseAddress = address;
            client.Timeout = TimeSpan.FromSeconds(15);
        }
    ).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddDbContext<ApplicationDbContext>(option => option.UseSqlite(
    builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=payments.db"));

var app = builder.Build();
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "User" })
        if (!await roles.RoleExistsAsync(role))
        {
            var result = await roles.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded) throw new InvalidOperationException("Could not initialize roles.");
        }
}

// Local operator command: reconcile one order using the same trusted service.
// Credentials remain in server configuration, never command-line arguments.
if (app.Configuration["verify-order"] is { Length: > 0 } orderId)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.OrderId == orderId);
    if (payment is null) { Console.WriteLine("Order not found."); return; }
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
    await scope.ServiceProvider.GetRequiredService<PaymentService>().VerifyAsync(payment, timeout.Token);
    var result = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == payment.Id);
    Console.WriteLine($"Order {result.OrderId}: {PaymentService.Status(result)} / {result.VerificationCode}");
    Console.WriteLine(result.VerificationMessage);
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Allow local HTTP without requiring a trusted development certificate.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();

public partial class Program { }
