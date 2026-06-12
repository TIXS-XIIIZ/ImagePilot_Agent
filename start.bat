@echo off
setlocal
cd /d "%~dp0"

echo Starting ImagePilot_Agent...
echo.
echo This window will stay open so you can see errors.
echo Dashboard: http://localhost:5173
echo API:       http://localhost:5000
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0start.ps1"

echo.
echo Waiting a few seconds for the servers to start...
timeout /t 5 /nobreak > nul
start "" "http://localhost:5173"

echo.
echo If the page does not open, look at the two terminal windows:
echo   one runs the .NET API
echo   one runs the Vite UI
echo.
pause
