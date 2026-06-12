$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# Some Windows setups expose both Path and PATH. Start-Process rejects that
# duplicate dictionary, so normalize only this script's process environment.
$pathValue = [Environment]::GetEnvironmentVariable("Path", "Process")
[Environment]::SetEnvironmentVariable("PATH", $null, "Process")
[Environment]::SetEnvironmentVariable("Path", $pathValue, "Process")

Start-Process cmd.exe `
    -ArgumentList "/k", "dotnet run --project `"$root\backend\ImagePilot.Api.csproj`"" `
    -WorkingDirectory $root

Start-Process cmd.exe `
    -ArgumentList "/k", "npm.cmd run dev -- --host localhost" `
    -WorkingDirectory "$root\frontend"

Write-Host "ImagePilot_Agent is starting."
Write-Host "Dashboard: http://localhost:5173"
Write-Host "API:       http://localhost:5000"
Write-Host "Two terminal windows were opened: one for the API and one for the UI."
