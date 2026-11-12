using Microsoft.EntityFrameworkCore;
using PaymentSystem.Data;
using PaymentSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddScoped<KhqrService>();
builder.Services.AddHttpClient<BakongService>(
    (serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var baseUrl = config["Bakong:BaseUrl"] ?? throw new InvalidOperationException("Bakong Url is missing.");
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(15);
        }
    );
builder.Services.AddDbContext<ApplicationDbContext>(option => option.UseSqlite(
    "Data Source=payments.db"
    ));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
