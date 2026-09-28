@echo off
REM ============================================================
REM Retail Platform — Start Frontend (Windows)
REM ============================================================

SET ROOT=%~dp0
SET OPS=%ROOT%frontend\ops-console
SET POS=%ROOT%frontend\pos-client

echo Installing Ops Console deps...
IF NOT EXIST "%OPS%\node_modules" (
  cd /D "%OPS%" && npm install
)

echo Installing POS Client deps...
IF NOT EXIST "%POS%\node_modules" (
  cd /D "%POS%" && npm install
)

echo.
echo Starting Angular apps...
START "Ops Console :4200" /D "%OPS%" CMD /C "npx ng serve --port 4200"
TIMEOUT /T 3 /NOBREAK >nul
START "POS Client :4201"  /D "%POS%" CMD /C "npx ng serve --port 4201"

echo.
echo   Ops Console ^> http://localhost:4200
echo   POS Client  ^> http://localhost:4201
echo.
pause
