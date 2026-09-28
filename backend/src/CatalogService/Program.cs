using CatalogService;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CatalogDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? "Host=localhost;Database=retail_catalog;Username=postgres;Password=postgres"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddPlatformInfrastructure(builder.Configuration, "catalog-svc");
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.EnsureCreatedAsync();
    await SeedCatalog.SeedAsync(db, logger);
}

app.UseCors();
app.MapOpenApi();
app.MapControllers();
app.Run();
