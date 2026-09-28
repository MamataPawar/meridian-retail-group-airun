using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentService;
using Platform.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Database=retail_payment;Username=postgres;Password=postgres"));

// Register all payment adapters
builder.Services.AddSingleton<IPaymentAdapter, CardAdapter>();
builder.Services.AddSingleton<IPaymentAdapter, KlarnaAdapter>();
builder.Services.AddSingleton<IPaymentAdapter, SatispayAdapter>();
builder.Services.AddSingleton<IPaymentAdapter, PayPayAdapter>();

builder.Services.AddScoped<PaymentOrchestrator>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddPlatformInfrastructure(builder.Configuration, "payment-svc", bus =>
{
    bus.AddConsumer<ProcessPaymentConsumer>();
    bus.AddConsumer<ScaChallengeCompletedConsumer>();
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseCors();
app.MapOpenApi();
app.MapControllers();
app.Run();
