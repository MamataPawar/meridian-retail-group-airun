using CartService;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CartDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Database=retail_cart;Username=postgres;Password=postgres"));

builder.Services.AddScoped<CartService.CartService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddPlatformInfrastructure(builder.Configuration, "cart-svc");

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CartDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseCors();
app.MapOpenApi();
app.MapControllers();
app.Run();
