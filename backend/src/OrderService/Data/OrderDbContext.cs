using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain;
using Platform.Infrastructure.Outbox;

namespace OrderService.Data;

public class OrderDbContext : SagaDbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();
    public DbSet<Fulfilment> Fulfilments => Set<Fulfilment>();
    public DbSet<PlaceOrderSagaState> PlaceOrderSagas => Set<PlaceOrderSagaState>();
    public DbSet<Platform.Infrastructure.Outbox.OutboxMessage> OutboxMessages =>
        Set<Platform.Infrastructure.Outbox.OutboxMessage>();

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new PlaceOrderSagaMap(); }
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Channel);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId);
            e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.OrderId);
            e.HasMany(x => x.Fulfilments).WithOne().HasForeignKey(x => x.OrderId);
        });

        mb.Entity<OrderLine>(e => e.HasKey(x => x.Id));
        mb.Entity<OrderLine>().Ignore(x => x.LineTotal);
        mb.Entity<OrderEvent>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.OrderId); });
        mb.Entity<Fulfilment>(e => e.HasKey(x => x.Id));
        mb.Entity<Platform.Infrastructure.Outbox.OutboxMessage>(e => e.HasKey(x => x.Id));
    }
}

public class PlaceOrderSagaMap : SagaClassMap<PlaceOrderSagaState>
{
    protected override void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<PlaceOrderSagaState> entity,
        ModelBuilder model)
    {
        entity.Property(x => x.CurrentState).HasMaxLength(64);
    }
}
