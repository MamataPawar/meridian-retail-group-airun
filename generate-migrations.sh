#!/usr/bin/env bash
# ============================================================
# Generate EF Core migrations for all services
# Run once after initial setup: ./generate-migrations.sh
# ============================================================
set -e

BACKEND="$(cd "$(dirname "${BASH_SOURCE[0]}")/backend" && pwd)"

echo "Installing EF Core tools..."
dotnet tool install --global dotnet-ef 2>/dev/null || dotnet tool update --global dotnet-ef

declare -a SERVICES=(
  "IdentityService:IdentityService.Data.IdentityDbContext"
  "CartService:CartService.CartDbContext"
  "CatalogService:CatalogService.CatalogDbContext"
  "InventoryService:InventoryService.Data.InventoryDbContext"
  "OrderService:OrderService.Data.OrderDbContext"
  "PaymentService:PaymentService.PaymentDbContext"
  "LoyaltyService:LoyaltyService.LoyaltyDbContext"
  "PromotionService:PromotionService.PromotionDbContext"
  "StoreService:StoreService.StoreDbContext"
)

for ENTRY in "${SERVICES[@]}"; do
  SVC="${ENTRY%%:*}"
  CTX="${ENTRY##*:}"
  DIR="$BACKEND/src/$SVC"

  echo ""
  echo "▶ Generating migration for $SVC..."
  cd "$DIR"
  dotnet ef migrations add InitialCreate \
    --context "$CTX" \
    --output-dir Migrations \
    -- 2>&1 || echo "  (skipped — migration may already exist)"
done

echo ""
echo "Done. You can now run: ./start-backend.sh"
