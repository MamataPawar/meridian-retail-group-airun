using Bogus;
using IdentityService.Domain;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Data;

public static class SeedData
{
    private static readonly string[] Countries = ["IT", "JP", "US", "FR", "DE", "GB"];
    private static readonly string[] Systems = ["Shopify-IT", ".NET-JP", "POS-US"];
    private static readonly string[] ConsentTypes = ["marketing", "analytics", "data_sharing"];
    private static readonly string[] Jurisdictions = ["GDPR-IT", "GDPR-FR", "GDPR-DE", "PDPA-JP", "CCPA-US", "GDPR-GB"];

    public static async Task SeedAsync(IdentityDbContext db, ILogger logger)
    {
        if (await db.Customers.AnyAsync()) { logger.LogInformation("Identity DB already seeded"); return; }

        logger.LogInformation("Seeding Identity DB...");
        Randomizer.Seed = new Random(42);

        var faker = new Faker<Customer>()
            .RuleFor(c => c.Id, f => Guid.NewGuid())
            .RuleFor(c => c.Email, f => f.Internet.Email())
            .RuleFor(c => c.Phone, f => f.Phone.PhoneNumber())
            .RuleFor(c => c.FirstName, f => f.Name.FirstName())
            .RuleFor(c => c.LastName, f => f.Name.LastName())
            .RuleFor(c => c.Country, f => f.PickRandom(Countries))
            .RuleFor(c => c.Status, _ => "active")
            .RuleFor(c => c.CreatedAt, f => f.Date.PastOffset(2).ToUniversalTime());

        var customers = faker.Generate(50_000);
        await db.Customers.AddRangeAsync(customers);

        // Create 8k fragmented accounts (duplicate email pairs)
        var fragmented = customers.Take(8_000).ToArray();
        var fragmentFaker = new Faker<Customer>()
            .RuleFor(c => c.Id, _ => Guid.NewGuid())
            .RuleFor(c => c.Email, (_, orig) => orig.Email) // same email = deterministic merge target
            .RuleFor(c => c.Phone, f => f.Phone.PhoneNumber())
            .RuleFor(c => c.FirstName, (_, orig) => orig.FirstName)
            .RuleFor(c => c.LastName, (_, orig) => orig.LastName)
            .RuleFor(c => c.Country, (_, orig) => orig.Country)
            .RuleFor(c => c.Status, _ => "active")
            .RuleFor(c => c.CreatedAt, f => f.Date.RecentOffset(365).ToUniversalTime());

        var fragments = fragmented
            .Select(orig =>
            {
                var clone = fragmentFaker.Generate();
                clone.Id = Guid.NewGuid();
                clone.Email = orig.Email;
                clone.FirstName = orig.FirstName;
                clone.LastName = orig.LastName;
                clone.Country = orig.Country;
                clone.Status = "active";
                return clone;
            })
            .ToList();
        await db.Customers.AddRangeAsync(fragments);

        // Identity links
        var linkFaker = new Faker();
        var allCustomers = customers.Concat(fragments).ToList();
        var links = allCustomers.Select(c => new IdentityLink
        {
            CustomerId = c.Id,
            System = linkFaker.PickRandom(Systems),
            ExternalId = linkFaker.Random.AlphaNumeric(12),
            Email = c.Email
        }).ToList();
        await db.IdentityLinks.AddRangeAsync(links);

        // Consent records
        var consentFaker = new Faker();
        var consents = allCustomers.SelectMany(c =>
            ConsentTypes.Select(ct => new ConsentRecord
            {
                CustomerId = c.Id,
                Jurisdiction = consentFaker.PickRandom(Jurisdictions),
                ConsentType = ct,
                Granted = consentFaker.Random.Bool(0.75f),
                RecordedAt = c.CreatedAt.AddMinutes(consentFaker.Random.Int(1, 60))
            })).ToList();
        await db.Consents.AddRangeAsync(consents);

        // 1,200 DSARs (90 overdue)
        var dsarCustomers = customers.Take(1_200).ToArray();
        var dsarFaker = new Faker();
        var dsarRequests = dsarCustomers.Select((c, i) =>
        {
            var requestedAt = i < 90
                ? DateTimeOffset.UtcNow.AddDays(-45)  // overdue
                : DateTimeOffset.UtcNow.AddDays(-dsarFaker.Random.Int(1, 20));

            return new DsarRequest
            {
                CustomerId = c.Id,
                Customer = c,
                Type = dsarFaker.PickRandom("export", "delete"),
                Jurisdiction = dsarFaker.PickRandom(Jurisdictions),
                Status = i < 90 ? "overdue" : dsarFaker.PickRandom("pending", "processing"),
                RequestedAt = requestedAt,
                DueBy = requestedAt.AddDays(30)
            };
        }).ToList();
        await db.DsarRequests.AddRangeAsync(dsarRequests);

        await db.SaveChangesAsync();
        logger.LogInformation("Identity DB seeded: {Customers} customers, {Fragments} fragments, {Dsars} DSARs",
            customers.Count, fragments.Count, dsarRequests.Count);
    }
}
