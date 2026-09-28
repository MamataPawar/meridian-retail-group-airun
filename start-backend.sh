#!/usr/bin/env bash
# ============================================================
# Retail Platform — Start All Backend Services
# Prerequisites: .NET 9 SDK, PostgreSQL running on localhost:5432
# ============================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$SCRIPT_DIR/backend"

echo "╔══════════════════════════════════════════════════════╗"
echo "║        Retail Platform — Backend Startup             ║"
echo "╚══════════════════════════════════════════════════════╝"

# Check prerequisites
command -v dotnet >/dev/null 2>&1 || { echo "ERROR: dotnet SDK not found. Install .NET 9 from https://dot.net"; exit 1; }
DOTNET_VER=$(dotnet --version | cut -d'.' -f1)
if [ "$DOTNET_VER" -lt 9 ]; then
  echo "WARNING: .NET $DOTNET_VER detected; .NET 9+ recommended"
fi

# Optional: check postgres
if command -v pg_isready >/dev/null 2>&1; then
  if ! pg_isready -q; then
    echo "ERROR: PostgreSQL is not running. Start it first."
    exit 1
  fi
fi

echo ""
echo "Starting 8 microservices..."
echo ""

declare -A SERVICES=(
  ["IdentityService"]="5001"
  ["CartService"]="5004"
  ["CatalogService"]="5005"
  ["InventoryService"]="5006"
  ["OrderService"]="5007"
  ["PaymentService"]="5008"
  ["LoyaltyService"]="5009"
  ["PromotionService"]="5010"
  ["StoreService"]="5011"
)

PIDS=()
LOG_DIR="$SCRIPT_DIR/logs"
mkdir -p "$LOG_DIR"

for SVC in "${!SERVICES[@]}"; do
  PORT="${SERVICES[$SVC]}"
  SVC_DIR="$BACKEND_DIR/src/$SVC"
  LOG_FILE="$LOG_DIR/${SVC}.log"

  if [ ! -d "$SVC_DIR" ]; then
    echo "SKIP: $SVC_DIR not found"
    continue
  fi

  echo "▶ Starting $SVC on :$PORT  (log → logs/${SVC}.log)"
  (cd "$SVC_DIR" && dotnet run --no-launch-profile 2>&1) > "$LOG_FILE" &
  PIDS+=($!)
done

echo ""
echo "All services launched. PIDs: ${PIDS[*]}"
echo ""
echo "Service URLs:"
echo "  Identity   → http://localhost:5001/openapi/v1.json"
echo "  Cart       → http://localhost:5004/openapi/v1.json"
echo "  Catalog    → http://localhost:5005/openapi/v1.json"
echo "  Inventory  → http://localhost:5006/openapi/v1.json"
echo "  Orders     → http://localhost:5007/openapi/v1.json"
echo "  Payment    → http://localhost:5008/openapi/v1.json"
echo "  Loyalty    → http://localhost:5009/openapi/v1.json"
echo "  Promotions → http://localhost:5010/openapi/v1.json"
echo "  Stores     → http://localhost:5011/openapi/v1.json"
echo ""
echo "Press Ctrl+C to stop all services."

# Wait and clean up on exit
cleanup() {
  echo ""
  echo "Stopping all services..."
  for PID in "${PIDS[@]}"; do
    kill "$PID" 2>/dev/null || true
  done
  echo "Done."
}
trap cleanup EXIT INT TERM

wait
