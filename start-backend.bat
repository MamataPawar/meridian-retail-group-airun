@echo off
REM ============================================================
REM Retail Platform — Start All Backend Services (Windows)
REM Prerequisites: .NET 9 SDK, PostgreSQL running on localhost:5432
REM ============================================================

echo ╔══════════════════════════════════════════════════╗
echo ║      Retail Platform — Backend Startup           ║
echo ╚══════════════════════════════════════════════════╝
echo.

WHERE dotnet >nul 2>nul
IF %ERRORLEVEL% NEQ 0 (
  echo ERROR: dotnet not found. Install from https://dot.net
  pause & exit /b 1
)

SET ROOT=%~dp0
SET BACKEND=%ROOT%backend\src
SET LOGS=%ROOT%logs
IF NOT EXIST "%LOGS%" MKDIR "%LOGS%"

echo Starting services...
echo.

START "IdentityService :5001"   /D "%BACKEND%\IdentityService"   CMD /C "dotnet run --no-launch-profile > %LOGS%\Identity.log 2>&1"
TIMEOUT /T 3 /NOBREAK >nul

START "CartService :5004"       /D "%BACKEND%\CartService"       CMD /C "dotnet run --no-launch-profile > %LOGS%\Cart.log 2>&1"
TIMEOUT /T 2 /NOBREAK >nul

START "CatalogService :5005"    /D "%BACKEND%\CatalogService"    CMD /C "dotnet run --no-launch-profile > %LOGS%\Catalog.log 2>&1"
TIMEOUT /T 2 /NOBREAK >nul

START "InventoryService :5006"  /D "%BACKEND%\InventoryService"  CMD /C "dotnet run --no-launch-profile > %LOGS%\Inventory.log 2>&1"
TIMEOUT /T 2 /NOBREAK >nul

START "OrderService :5007"      /D "%BACKEND%\OrderService"      CMD /C "dotnet run --no-launch-profile > %LOGS%\Order.log 2>&1"
TIMEOUT /T 2 /NOBREAK >nul

START "PaymentService :5008"    /D "%BACKEND%\PaymentService"    CMD /C "dotnet run --no-launch-profile > %LOGS%\Payment.log 2>&1"
TIMEOUT /T 2 /NOBREAK >nul

START "LoyaltyService :5009"    /D "%BACKEND%\LoyaltyService"    CMD /C "dotnet run --no-launch-profile > %LOGS%\Loyalty.log 2>&1"
TIMEOUT /T 2 /NOBREAK >nul

START "PromotionService :5010"  /D "%BACKEND%\PromotionService"  CMD /C "dotnet run --no-launch-profile > %LOGS%\Promotion.log 2>&1"
TIMEOUT /T 2 /NOBREAK >nul

START "StoreService :5011"      /D "%BACKEND%\StoreService"      CMD /C "dotnet run --no-launch-profile > %LOGS%\Store.log 2>&1"

echo.
echo All services launched in separate windows.
echo.
echo Service URLs:
echo   Identity   ^> http://localhost:5001/openapi/v1.json
echo   Cart       ^> http://localhost:5004/openapi/v1.json
echo   Catalog    ^> http://localhost:5005/openapi/v1.json
echo   Inventory  ^> http://localhost:5006/openapi/v1.json
echo   Orders     ^> http://localhost:5007/openapi/v1.json
echo   Payment    ^> http://localhost:5008/openapi/v1.json
echo   Loyalty    ^> http://localhost:5009/openapi/v1.json
echo   Promotions ^> http://localhost:5010/openapi/v1.json
echo   Stores     ^> http://localhost:5011/openapi/v1.json
echo.
echo Close the individual service windows to stop them.
pause
