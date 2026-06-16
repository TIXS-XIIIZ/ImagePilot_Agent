@echo off
setlocal
cd /d "%~dp0"
echo Starting ImagePilot Control Panel...
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0launcher.ps1"
