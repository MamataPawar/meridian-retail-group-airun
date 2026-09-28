using IdentityService.Domain;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Outbox;

namespace IdentityService.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<IdentityLink> IdentityLinks => Set<IdentityLink>();
    public DbSet<CustomerAddress> Addresses => Set<CustomerAddress>();
    public DbSet<ConsentRecord> Consents => Set<ConsentRecord>();
    public DbSet<MergeAuditEntry> MergeAudits => Set<MergeAuditEntry>();
    public DbSet<MergeCandidate> MergeCandidates => Set<MergeCandidate>();
    public DbSet<DsarRequest> DsarRequests => Set<DsarRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => x.Status);
            e.HasMany(x => x.IdentityLinks).WithOne(x => x.Customer).HasForeignKey(x => x.CustomerId);
            e.HasMany(x => x.Addresses).WithOne().HasForeignKey(x => x.CustomerId);
            e.HasMany(x => x.Consents).WithOne().HasForeignKey(x => x.CustomerId);
            e.HasMany(x => x.MergeAudits).WithOne().HasForeignKey(x => x.CanonicalCustomerId);
        });

        mb.Entity<IdentityLink>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.System, x.ExternalId }).IsUnique();
        });

        mb.Entity<MergeCandidate>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.CustomerA).WithMany().HasForeignKey(x => x.CustomerAId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CustomerB).WithMany().HasForeignKey(x => x.CustomerBId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ConfidenceScore);
        });

        mb.Entity<DsarRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DueBy);
        });

        mb.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProcessedAt);
        });
    }
}
