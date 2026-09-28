#!/usr/bin/env bash
# ============================================================
# Retail Platform — Start Frontend Apps
# Prerequisites: Node.js 18+, Angular CLI (npm i -g @angular/cli)
# ============================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FRONTEND_DIR="$SCRIPT_DIR/frontend"

echo "╔══════════════════════════════════════════════════════╗"
echo "║        Retail Platform — Frontend Startup            ║"
echo "╚══════════════════════════════════════════════════════╝"

command -v node >/dev/null 2>&1 || { echo "ERROR: Node.js not found. Install from https://nodejs.org"; exit 1; }
command -v npm  >/dev/null 2>&1 || { echo "ERROR: npm not found."; exit 1; }

echo ""
echo "Installing dependencies (first run may take a few minutes)..."
echo ""

# Install ops-console deps
echo "▶ ops-console..."
cd "$FRONTEND_DIR/ops-console"
if [ ! -d "node_modules" ]; then
  npm install --silent
fi

# Install pos-client deps
echo "▶ pos-client..."
cd "$FRONTEND_DIR/pos-client"
if [ ! -d "node_modules" ]; then
  npm install --silent
fi

echo ""
echo "Starting Angular dev servers..."
echo ""

PIDS=()

cd "$FRONTEND_DIR/ops-console"
echo "▶ Ops Console  → http://localhost:4200"
npx ng serve --port 4200 --open=false &
PIDS+=($!)

cd "$FRONTEND_DIR/pos-client"
echo "▶ POS Client   → http://localhost:4201"
npx ng serve --port 4201 --open=false &
PIDS+=($!)

echo ""
echo "Both apps starting. Open in browser:"
echo "  Ops Console → http://localhost:4200"
echo "  POS Client  → http://localhost:4201"
echo ""
echo "Press Ctrl+C to stop."

cleanup() {
  echo "Stopping frontend servers..."
  for PID in "${PIDS[@]}"; do
    kill "$PID" 2>/dev/null || true
  done
}
trap cleanup EXIT INT TERM
wait
