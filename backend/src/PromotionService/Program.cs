using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Extensions;
using PromotionService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PromotionDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Database=retail_promotion;Username=postgres;Password=postgres"));

builder.Services.AddSingleton<IPromotionRule, PercentageRule>();
builder.Services.AddSingleton<IPromotionRule, FixedDiscountRule>();
builder.Services.AddScoped<PromotionEngine>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddPlatformInfrastructure(builder.Configuration, "promotion-svc");
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
    await db.Database.EnsureCreatedAsync();
    // Seed a few sample promotions
    if (!await db.Promotions.AnyAsync())
    {
        var now = DateTimeOffset.UtcNow;
        db.Promotions.AddRange(
            new Promotion { Name = "Summer Sale IT", Code = "SUMMER-IT", Type = "percentage", DiscountValue = 20, Country = "IT", StartsAt = now.AddDays(-5), EndsAt = now.AddDays(30) },
            new Promotion { Name = "Welcome10", Code = "WELCOME10", Type = "percentage", DiscountValue = 10, StartsAt = now.AddDays(-30), EndsAt = now.AddDays(60), StackingAllowed = true },
            new Promotion { Name = "Flash IT 20% OFF", Code = "FLASH-IT", Type = "percentage", DiscountValue = 20, Country = "IT", StartsAt = now.AddDays(-2), EndsAt = now.AddDays(5) }, // conflict with SUMMER-IT
            new Promotion { Name = "EUR5 Fixed", Code = "EUR5OFF", Type = "fixed", DiscountValue = 5, MinOrderValue = 50, StartsAt = now.AddDays(-10), EndsAt = now.AddDays(20) }
        );
        await db.SaveChangesAsync();
    }
}

app.UseCors();
app.MapOpenApi();
app.MapControllers();
app.Run();
