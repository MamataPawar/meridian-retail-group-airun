using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Controllers;
using OrderService.Data;
using OrderService.Domain;
using OrderService.Sagas;
using Platform.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrderDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Database=retail_orders;Username=postgres;Password=postgres"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddPlatformInfrastructure(builder.Configuration, "order-svc", bus =>
{
    bus.AddSagaStateMachine<PlaceOrderSaga, PlaceOrderSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.AddDbContext<DbContext, OrderDbContext>();
            r.UsePostgres();
        });

    bus.AddConsumer<OrderConfirmedConsumer>();
    bus.AddConsumer<OrderCancelledConsumer>();
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedOrders.SeedAsync(db, scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
}

app.UseCors();
app.MapOpenApi();
app.MapControllers();
app.Run();
