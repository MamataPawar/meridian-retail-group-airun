using InventoryService.Controllers;
using InventoryService.Data;
using InventoryService.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<InventoryDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Database=retail_inventory;Username=postgres;Password=postgres"));

builder.Services.AddScoped<IInventoryService, InventoryServiceImpl>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddPlatformInfrastructure(builder.Configuration, "inventory-svc", bus =>
{
    bus.AddConsumer<ReserveInventoryConsumer>();
    bus.AddConsumer<ReleaseInventoryConsumer>();
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// Background job: expire reservations every minute
builder.Services.AddHostedService<ReservationExpiryJob>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.SeedAsync(db, logger);
}

app.UseCors();
app.MapOpenApi();
app.MapControllers();
app.Run();

// Hosted service for TTL expiry
public class ReservationExpiryJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReservationExpiryJob> _logger;

    public ReservationExpiryJob(IServiceProvider services, ILogger<ReservationExpiryJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            try
            {
                using var scope = _services.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IInventoryService>();
                await svc.ExpireReservationsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error expiring reservations");
            }
        }
    }
}
