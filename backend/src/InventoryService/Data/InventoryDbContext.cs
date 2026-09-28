using InventoryService.Domain;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Outbox;

namespace InventoryService.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<StockPosition> StockPositions => Set<StockPosition>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<SapSyncLog> SapSyncLogs => Set<SapSyncLog>();
    public DbSet<DriftEvent> DriftEvents => Set<DriftEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<StockPosition>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SkuId, x.StoreId }).IsUnique();
            e.HasIndex(x => x.ConfidenceScore);
            e.HasIndex(x => x.HiddenFromClickAndCollect);
            e.HasMany(x => x.Reservations).WithOne(r => r.StockPosition)
                .HasPrincipalKey(s => new { s.SkuId, s.StoreId })
                .HasForeignKey(r => new { r.SkuId, r.StoreId });
            // Ignore computed property
            e.Ignore(x => x.Available);
        });

        mb.Entity<Reservation>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ExpiresAt);
        });

        mb.Entity<SapSyncLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.Drift);
        });

        mb.Entity<DriftEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StoreId, x.Resolved });
        });

        mb.Entity<OutboxMessage>(e => e.HasKey(x => x.Id));
    }
}
