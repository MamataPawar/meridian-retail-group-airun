using IdentityService.Data;
using IdentityService.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<IdentityDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Database=retail_identity;Username=postgres;Password=postgres"));

builder.Services.AddScoped<IMergeEngine, MergeEngine>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddPlatformInfrastructure(builder.Configuration, "identity-svc", bus =>
{
    // Register consumers here when added
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// Auto-migrate and seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try { await db.Database.MigrateAsync(); } catch { await db.Database.EnsureCreatedAsync(); }
    await SeedData.SeedAsync(db, logger);
}

app.UseCors();
app.MapOpenApi();
app.MapControllers();
app.Run();
