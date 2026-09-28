using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Extensions;
using StoreService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<StoreDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Database=retail_stores;Username=postgres;Password=postgres"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddPlatformInfrastructure(builder.Configuration, "store-svc");
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.EnsureCreatedAsync();
    await SeedStores.SeedAsync(db, logger);
}

app.UseCors();
app.MapOpenApi();
app.MapControllers();
app.Run();
