@echo off
title Mandi Dost - Backend (keep this window open)
echo Starting Mandi Dost backend on http://0.0.0.0:5080 ...
echo Keep this window open while testing. Close it to stop the backend.
echo.
rem If an older backend is still running (another window or a test run), close it first.
taskkill /IM MandiDost.Api.exe /F >nul 2>&1
cd /d "%~dp0src\MandiDost.Api"
dotnet run --launch-profile lan
echo.
echo The backend stopped. If it says "address already in use", another program is using port 5080.
pause
