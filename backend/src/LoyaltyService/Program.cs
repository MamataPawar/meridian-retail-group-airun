using LoyaltyService;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LoyaltyDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Database=retail_loyalty;Username=postgres;Password=postgres"));

builder.Services.AddScoped<LoyaltySvc>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddPlatformInfrastructure(builder.Configuration, "loyalty-svc");
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LoyaltyDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.EnsureCreatedAsync();
    await SeedLoyalty.SeedAsync(db, logger);
}

app.UseCors();
app.MapOpenApi();
app.MapControllers();
app.Run();
