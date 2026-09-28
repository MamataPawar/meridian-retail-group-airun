using Bogus;
using InventoryService.Domain;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Data;

public static class SeedData
{
    private static readonly string[] PhantomStores = Enumerable.Range(1, 12).Select(i => $"STORE-{i:D4}").ToArray();
    private static readonly string[] AllStores = Enumerable.Range(1, 200).Select(i => $"STORE-{i:D4}").ToArray();

    public static async Task SeedAsync(InventoryDbContext db, ILogger logger)
    {
        if (await db.StockPositions.AnyAsync()) { logger.LogInformation("Inventory DB already seeded"); return; }

        logger.LogInformation("Seeding Inventory DB...");
        Randomizer.Seed = new Random(42);
        var faker = new Faker();

        var positions = new List<StockPosition>();
        var skuIds = Enumerable.Range(1, 20_000).Select(_ => Guid.NewGuid()).ToArray();
        var storeSample = AllStores.Take(200).ToArray();

        foreach (var store in storeSample)
        {
            var isPhantom = PhantomStores.Contains(store);
            foreach (var skuId in skuIds.Take(faker.Random.Int(50, 200))) // each store stocks a subset
            {
                var onHand = faker.Random.Int(0, 150);
                var confidence = isPhantom
                    ? faker.Random.Double(0.2, 0.55) // phantom stores: low confidence
                    : faker.Random.Double(0.7, 1.0);

                positions.Add(new StockPosition
                {
                    SkuId = skuId,
                    StoreId = store,
                    OnHand = onHand,
                    Reserved = faker.Random.Int(0, Math.Max(0, onHand - 5)),
                    Buffer = 2,
                    ConfidenceScore = confidence,
                    HiddenFromClickAndCollect = confidence < 0.6,
                    LastSapSyncAt = DateTimeOffset.UtcNow.AddHours(-faker.Random.Int(1, 48))
                });
            }
        }

        // Batch insert
        const int batchSize = 1000;
        for (int i = 0; i < positions.Count; i += batchSize)
        {
            await db.StockPositions.AddRangeAsync(positions.Skip(i).Take(batchSize));
            await db.SaveChangesAsync();
        }

        logger.LogInformation("Inventory DB seeded: {Count} stock positions ({PhantomStores} phantom-stock stores)",
            positions.Count, PhantomStores.Length);
    }
}
