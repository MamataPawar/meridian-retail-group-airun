using Bogus;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain;

namespace OrderService.Data;

public static class SeedOrders
{
    private static readonly string[] Channels = ["web", "pos", "app"];
    private static readonly string[] Statuses = ["confirmed", "shipped", "delivered", "cancelled", "pending"];
    private static readonly string[] Countries = ["IT", "JP", "US", "FR", "DE", "GB"];
    private static readonly string[] Currencies = ["EUR", "JPY", "USD", "GBP"];

    public static async Task SeedAsync(OrderDbContext db, ILogger logger)
    {
        if (await db.Orders.AnyAsync()) { logger.LogInformation("Order DB already seeded"); return; }

        logger.LogInformation("Seeding Order DB...");
        Randomizer.Seed = new Random(42);
        var faker = new Faker();

        var orders = new List<Order>();
        for (int i = 0; i < 80_000; i++)
        {
            var channel = faker.PickRandom(Channels);
            var isCnC = channel == "pos" && faker.Random.Bool(0.15f);
            var country = faker.PickRandom(Countries);
            var currency = country switch { "JP" => "JPY", "US" => "USD", "GB" => "GBP", _ => "EUR" };
            var status = faker.PickRandom(Statuses);
            var createdAt = faker.Date.PastOffset(1).ToUniversalTime();

            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                Channel = channel,
                StoreId = channel == "pos" ? $"STORE-{faker.Random.Int(1, 200):D4}" : null,
                Country = country,
                TotalAmount = faker.Random.Decimal(10, 2000),
                Currency = currency,
                Status = status,
                IsClickAndCollect = isCnC,
                PickupStatus = isCnC ? faker.PickRandom("reserved", "collected", "expired") : null,
                PickupExpiresAt = isCnC ? createdAt.AddHours(48) : null,
                SagaState = status == "cancelled" ? "Cancelled" : status == "confirmed" ? "Confirmed" : null,
                CreatedAt = createdAt,
                UpdatedAt = createdAt.AddMinutes(faker.Random.Int(1, 120))
            };

            var lineCount = faker.Random.Int(1, 5);
            for (int l = 0; l < lineCount; l++)
            {
                order.Lines.Add(new OrderLine
                {
                    SkuId = Guid.NewGuid(),
                    SkuName = $"Product {faker.Commerce.ProductName()}",
                    Quantity = faker.Random.Int(1, 4),
                    UnitPrice = faker.Random.Decimal(5, 500),
                    VatRate = country is "IT" or "FR" or "DE" ? 0.22m : 0.10m
                });
            }

            orders.Add(order);
        }

        const int batch = 500;
        for (int i = 0; i < orders.Count; i += batch)
        {
            await db.Orders.AddRangeAsync(orders.Skip(i).Take(batch));
            await db.SaveChangesAsync();
        }

        logger.LogInformation("Order DB seeded: {Count} orders", orders.Count);
    }
}
