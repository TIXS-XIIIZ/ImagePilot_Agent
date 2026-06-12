$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# Some Windows setups expose both Path and PATH. Start-Process rejects that
# duplicate dictionary, so normalize only this script's process environment.
$pathValue = [Environment]::GetEnvironmentVariable("Path", "Process")
[Environment]::SetEnvironmentVariable("PATH", $null, "Process")
[Environment]::SetEnvironmentVariable("Path", $pathValue, "Process")

Start-Process dotnet -ArgumentList "run --project `"$root\backend\ImagePilot.Api.csproj`"" -WorkingDirectory $root -WindowStyle Hidden
Start-Process cmd.exe -ArgumentList "/c", "npm run dev -- --host localhost" -WorkingDirectory "$root\frontend" -WindowStyle Hidden

Write-Host "ImagePilot_Agent is starting."
Write-Host "Dashboard: http://localhost:5173"
Write-Host "API:       http://localhost:5000"
