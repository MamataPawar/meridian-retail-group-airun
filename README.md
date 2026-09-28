# Retail Platform — Runnable Codebase

A full-stack retail system: **8 .NET 9 microservices** + **2 Angular 19 apps**, all runnable locally without Docker.

---

## Architecture

```
┌──────────────────────────────────────────────┐
│              Angular Frontends               │
│  Ops Console :4200    POS Client :4201       │
└─────────────────────┬────────────────────────┘
                       │ HTTP REST
┌──────────────────────▼────────────────────────────────────────────────┐
│                        Microservices                                   │
│                                                                        │
│  IdentityService :5001   CartService :5004   CatalogService :5005     │
│  InventoryService :5006  OrderService :5007  PaymentService :5008     │
│  LoyaltyService :5009    PromotionService :5010  StoreService :5011   │
└──────────┬──────────────────────────────────────────────┬─────────────┘
           │ EF Core / Npgsql                             │ MassTransit
┌──────────▼────────────┐                    ┌────────────▼─────────────┐
│ PostgreSQL (9 DBs)     │                    │ RabbitMQ (optional)      │
│ retail_identity        │                    │ localhost:5672           │
│ retail_cart            │                    │ (services work without   │
│ retail_catalog         │                    │  it, events are no-ops)  │
│ retail_inventory       │                    └──────────────────────────┘
│ retail_orders          │
│ retail_payment         │
│ retail_loyalty         │
│ retail_promotion       │
│ retail_stores          │
└────────────────────────┘
```

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 9.0+ | https://dot.net |
| Node.js | 18+ | https://nodejs.org |
| PostgreSQL | 14+ | Running on localhost:5432 |
| RabbitMQ | 3.12+ | **Optional** — services degrade gracefully without it |

---

## Quick Start

### Step 1 — Create Postgres databases

```bash
psql -U postgres -f setup-databases.sql
```

Or manually:
```sql
CREATE DATABASE retail_identity;
CREATE DATABASE retail_cart;
CREATE DATABASE retail_catalog;
CREATE DATABASE retail_inventory;
CREATE DATABASE retail_orders;
CREATE DATABASE retail_payment;
CREATE DATABASE retail_loyalty;
CREATE DATABASE retail_promotion;
CREATE DATABASE retail_stores;
```

### Step 2 — Start all backend services

**Mac / Linux:**
```bash
chmod +x start-backend.sh start-frontend.sh
./start-backend.sh
```

**Windows:**
```
start-backend.bat
```

> First run: EF Core migrations run automatically and seed data is loaded.  
> **Seeding takes 2-5 minutes** (50k customers, 20k SKUs, 80k orders, etc.)

### Step 3 — Start frontend apps

In a separate terminal:

**Mac / Linux:**
```bash
./start-frontend.sh
```

**Windows:**
```
start-frontend.bat
```

### Step 4 — Open in browser

| App | URL |
|-----|-----|
| Ops Console | http://localhost:4200 |
| POS Client | http://localhost:4201 |

---

## Service Reference

| Service | Port | DB | Key Features |
|---------|------|----|-------------|
| IdentityService | 5001 | retail_identity | Deterministic + probabilistic merge, DSAR, consents |
| CartService | 5004 | retail_cart | Cross-channel cart, conflict resolver |
| CatalogService | 5005 | retail_catalog | 20k SKUs, 22-country price books, VAT/GST |
| InventoryService | 5006 | retail_inventory | ATP calc, confidence scoring, drift detection |
| OrderService | 5007 | retail_orders | PlaceOrder saga, Click & Collect, saga inspector |
| PaymentService | 5008 | retail_payment | IPaymentAdapter (card/Klarna/Satispay/PayPay), SCA |
| LoyaltyService | 5009 | retail_loyalty | Unified ledger, account merge, tier management |
| PromotionService | 5010 | retail_promotion | Rule engine, stacking rules, conflict detector |
| StoreService | 5011 | retail_stores | 200 stores, 600 terminals, heartbeat, offline queue |

---

## Ops Console Screens

| Screen | Route | Description |
|--------|-------|-------------|
| Dashboard | / | Live stats, recent orders, drift alerts |
| Customers | /customers | Search 50k customers, filter by country |
| Customer 360 | /customers/:id | Profile, orders, consents, merge audit, loyalty |
| Identity Merge Queue | /merge-queue | 40k probabilistic candidates, side-by-side compare, approve/reject/undo |
| Inventory | /inventory | Stock positions with confidence scores, drift alerts, SAP sync log |
| Orders | /orders | 80k orders, channel/status filter |
| Order Detail | /orders/:id | Saga inspector visual, order lines, C&C status |
| Loyalty | /loyalty | Fragmented accounts (210), merge with point transfer |
| Stores & POS | /stores | 200 stores, terminal health, offline queue depth |
| Promotions | /promotions | Active promos, stacking rules, conflict detector |

---

## POS Client Screens

| Screen | Route | Description |
|--------|-------|-------------|
| Sell | /sell | Scan items, qty, promos, park/resume, void |
| Customer Lookup | /customer | Scan loyalty card / NFC / email, <2s, tier + points |
| Resume Cart | /resume-cart | Pick up web cart, cross-channel conflict resolver |
| Payment | /payment | Card + local methods (Satispay, Klarna, PayPay), SCA challenge, offline queue |
| Click & Collect | /click-collect | Per-store pickup queue, identity verify, hand over / cancel |
| Offline Mode | /offline | IndexedDB queue depth, force sync, demo controls |

---

## Seed Data

| Entity | Count | Notable |
|--------|-------|---------|
| Customers | 50,000 | 8,000 fragmented (duplicate email pairs) |
| Merge candidates | 40,000 | probabilistic queue |
| DSARs | 1,200 | 90 overdue |
| SKUs | 20,000 | 20 country price books each |
| Stock positions | ~200,000 | 12 phantom-stock stores (low confidence) |
| Orders | 80,000 | 12 months history, all channels |
| Stores | 200 | 6 chronically offline, C&C enabled |
| Terminals | 600 | 3 per store, online/offline status |
| Loyalty accounts | 50,210 | 210 fragmented (same customer, 2 accounts) |
| Promotions | 4 | Includes 1 region conflict (IT, same dates) |

---

## Connection Strings

All services default to:
```
Host=localhost;Database=retail_<service>;Username=postgres;Password=postgres
```

Override in `appsettings.json` or via environment variable:
```bash
ConnectionStrings__Default="Host=localhost;Database=retail_identity;Username=myuser;Password=mypass"
```

---

## RabbitMQ (Optional)

If RabbitMQ is not running, services start but event publishing is silently skipped. Install via:
```bash
# Mac
brew install rabbitmq && brew services start rabbitmq

# Ubuntu
sudo apt-get install rabbitmq-server

# Windows
# Download from https://www.rabbitmq.com/download.html
```

Management UI: http://localhost:15672 (guest/guest)

---

## Key Design Patterns

- **Outbox + Inbox** — `OutboxMessage` table per service prevents dual-write
- **`[Pii]` attribute** — marks fields for encryption + log redaction
- **Feature flags table** — country/channel/capability routing
- **`IPaymentAdapter`** — plug in new payment methods without touching order flow
- **`IPromotionRule`** — plug in new promotion types
- **PlaceOrder saga** — full state machine with compensation (inventory release → payment refund)
- **Confidence scoring** — inventory positions below 0.6 hidden from C&C automatically
- **IndexedDB offline queue** — POS sells continue during network loss, auto-reconcile on reconnect

---

## OpenAPI Docs

Each service exposes OpenAPI at `/openapi/v1.json`. Browse any service:
- http://localhost:5001/openapi/v1.json (Identity)
- http://localhost:5006/openapi/v1.json (Inventory)
- http://localhost:5007/openapi/v1.json (Orders + Saga)

---

## Running Individual Services

```bash
# Example: run just the Identity service
cd backend/src/IdentityService
dotnet run
```

```bash
# Example: run just the Ops Console
cd frontend/ops-console
npm install
npx ng serve --port 4200
```
