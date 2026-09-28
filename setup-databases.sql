-- ============================================================
-- Retail Platform — Database Setup
-- Run this once as postgres superuser before starting services
-- Usage: psql -U postgres -f setup-databases.sql
-- ============================================================

-- Create all 8 service databases
CREATE DATABASE retail_identity;
CREATE DATABASE retail_cart;
CREATE DATABASE retail_catalog;
CREATE DATABASE retail_inventory;
CREATE DATABASE retail_orders;
CREATE DATABASE retail_payment;
CREATE DATABASE retail_loyalty;
CREATE DATABASE retail_promotion;
CREATE DATABASE retail_stores;

-- Optional: create a dedicated app user
-- CREATE USER retail_app WITH PASSWORD 'retail_app_password';
-- GRANT ALL PRIVILEGES ON DATABASE retail_identity  TO retail_app;
-- GRANT ALL PRIVILEGES ON DATABASE retail_cart      TO retail_app;
-- GRANT ALL PRIVILEGES ON DATABASE retail_catalog   TO retail_app;
-- GRANT ALL PRIVILEGES ON DATABASE retail_inventory TO retail_app;
-- GRANT ALL PRIVILEGES ON DATABASE retail_orders    TO retail_app;
-- GRANT ALL PRIVILEGES ON DATABASE retail_payment   TO retail_app;
-- GRANT ALL PRIVILEGES ON DATABASE retail_loyalty   TO retail_app;
-- GRANT ALL PRIVILEGES ON DATABASE retail_promotion TO retail_app;
-- GRANT ALL PRIVILEGES ON DATABASE retail_stores    TO retail_app;

\echo 'Databases created successfully.'
\echo 'EF Core migrations and seeding run automatically on first service start.'
