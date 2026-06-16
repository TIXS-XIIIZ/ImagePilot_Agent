Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$logsDir = "$root\logs"
if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Force -Path $logsDir | Out-Null }

$backendLog = "$logsDir\backend.log"
$frontendLog = "$logsDir\frontend.log"

$global:backendProcess = $null
$global:frontendProcess = $null

[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="ImagePilot Control Panel" Height="250" Width="550" Background="#f8f9fa" WindowStartupLocation="CenterScreen">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        
        <TextBlock Text="ImagePilot Control Panel" FontSize="24" FontWeight="Bold" HorizontalAlignment="Center" Margin="0,0,0,20" Foreground="#2c3e50" />
        
        <TextBlock Name="txtStatus" Grid.Row="1" Text="Status: Stopped" FontSize="16" FontWeight="SemiBold" HorizontalAlignment="Center" Margin="0,0,0,20" Foreground="#7f8c8d" />

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Center">
            <Button Name="btnStart" Content="Start" Width="100" Height="35" Margin="10,0" Background="#2ecc71" Foreground="White" FontWeight="Bold" BorderThickness="0" Cursor="Hand"/>
            <Button Name="btnRestart" Content="Restart" Width="100" Height="35" Margin="10,0" Background="#3498db" Foreground="White" FontWeight="Bold" BorderThickness="0" Cursor="Hand"/>
            <Button Name="btnStop" Content="Stop" Width="100" Height="35" Margin="10,0" Background="#e74c3c" Foreground="White" FontWeight="Bold" BorderThickness="0" Cursor="Hand"/>
        </StackPanel>
        
        <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="0,20,0,0">
            <Button Name="btnOpenWeb" Content="Open Web UI" Width="120" Height="30" Margin="5,0" Background="#95a5a6" Foreground="White" BorderThickness="0" Cursor="Hand"/>
            <Button Name="btnExit" Content="Exit Launcher" Width="120" Height="30" Margin="5,0" Background="#7f8c8d" Foreground="White" BorderThickness="0" Cursor="Hand"/>
        </StackPanel>
    </Grid>
</Window>
"@

$reader = (New-Object System.Xml.XmlNodeReader $xaml)
$window = [Windows.Markup.XamlReader]::Load($reader)

$btnStart = $window.FindName("btnStart")
$btnRestart = $window.FindName("btnRestart")
$btnStop = $window.FindName("btnStop")
$btnExit = $window.FindName("btnExit")
$btnOpenWeb = $window.FindName("btnOpenWeb")
$txtStatus = $window.FindName("txtStatus")

function Update-Status {
    param([string]$Status, [string]$Color)
    $txtStatus.Text = "Status: $Status"
    $txtStatus.Foreground = $Color
}

function Stop-Services {
    Update-Status "Stopping..." "#e67e22"
    
    # Try gracefully stopping processes
    if ($global:backendProcess -and -not $global:backendProcess.HasExited) {
        $global:backendProcess.Kill()
    }
    if ($global:frontendProcess -and -not $global:frontendProcess.HasExited) {
        $global:frontendProcess.Kill()
    }
    
    # Force kill any lingering node.exe and dotnet.exe processes launched from our root
    Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match "dotnet run" -and $_.CommandLine -match "ImagePilot" } | Invoke-CimMethod -MethodName Terminate | Out-Null
    Get-CimInstance Win32_Process | Where-Object { $_.ProcessName -eq "ImagePilot.Api" } | Invoke-CimMethod -MethodName Terminate | Out-Null
    Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match "vite" -and $_.CommandLine -match "frontend" } | Invoke-CimMethod -MethodName Terminate | Out-Null
    
    $global:backendProcess = $null
    $global:frontendProcess = $null
    
    Update-Status "Stopped" "#7f8c8d"
    $btnStart.IsEnabled = $true
    $btnStop.IsEnabled = $false
    $btnRestart.IsEnabled = $false
}

function Start-Services {
    if ($global:backendProcess -ne $null -or $global:frontendProcess -ne $null) {
        return
    }
    
    Update-Status "Starting..." "#f39c12"
    $btnStart.IsEnabled = $false
    
    # Clear logs
    if (Test-Path $backendLog) { Clear-Content $backendLog -ErrorAction SilentlyContinue }
    if (Test-Path $frontendLog) { Clear-Content $frontendLog -ErrorAction SilentlyContinue }

    # Start Backend
    $backendInfo = New-Object System.Diagnostics.ProcessStartInfo
    $backendInfo.FileName = "cmd.exe"
    $backendInfo.Arguments = "/c `"dotnet run --project `"$root\backend\ImagePilot.Api.csproj`" > `"$backendLog`" 2>&1`""
    $backendInfo.WorkingDirectory = $root
    $backendInfo.WindowStyle = "Hidden"
    $backendInfo.CreateNoWindow = $true
    
    $global:backendProcess = [System.Diagnostics.Process]::Start($backendInfo)

    # Start Frontend
    $frontendInfo = New-Object System.Diagnostics.ProcessStartInfo
    $frontendInfo.FileName = "cmd.exe"
    $frontendInfo.Arguments = "/c `"npm.cmd run dev -- --host localhost > `"$frontendLog`" 2>&1`""
    $frontendInfo.WorkingDirectory = "$root\frontend"
    $frontendInfo.WindowStyle = "Hidden"
    $frontendInfo.CreateNoWindow = $true

    $global:frontendProcess = [System.Diagnostics.Process]::Start($frontendInfo)

    Update-Status "Running" "#27ae60"
    $btnStop.IsEnabled = $true
    $btnRestart.IsEnabled = $true
}

$btnStart.Add_Click({ Start-Services })
$btnStop.Add_Click({ Stop-Services })
$btnRestart.Add_Click({
    Stop-Services
    Start-Sleep -Seconds 1
    Start-Services
})
$btnOpenWeb.Add_Click({
    Start-Process "http://localhost:5173"
})
$btnExit.Add_Click({
    Stop-Services
    $window.Close()
})

$window.Add_Closed({
    Stop-Services
})

# Initial State
Update-Status "Stopped" "#7f8c8d"
$btnStop.IsEnabled = $false
$btnRestart.IsEnabled = $false

# Show Window
$window.ShowDialog() | Out-Null
