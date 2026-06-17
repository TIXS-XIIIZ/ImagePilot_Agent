Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$logsDir = "$root\logs"
if (-not (Test-Path $logsDir)) { New-Item -ItemType Directory -Force -Path $logsDir | Out-Null }

$backendLog  = "$logsDir\backend.log"
$frontendLog = "$logsDir\frontend.log"

$global:backendProcess  = $null
$global:frontendProcess = $null
$global:watcherTimer    = $null

[xml]$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="ImagePilot Control Panel" Height="260" Width="560"
        Background="#1e1e2e" WindowStartupLocation="CenterScreen" ResizeMode="NoResize">
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Text="ImagePilot Agent" FontSize="22" FontWeight="Bold"
                   HorizontalAlignment="Center" Margin="0,0,0,12"
                   Foreground="#cdd6f4" FontFamily="Segoe UI"/>

        <Border Grid.Row="1" CornerRadius="8" Margin="0,0,0,16" Padding="12,8">
            <Border.Background><SolidColorBrush Color="#313244"/></Border.Background>
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                <Ellipse Name="statusDot" Width="12" Height="12" Margin="0,0,8,0" VerticalAlignment="Center">
                    <Ellipse.Fill><SolidColorBrush Color="#6c7086"/></Ellipse.Fill>
                </Ellipse>
                <TextBlock Name="txtStatus" Text="Stopped" FontSize="14" FontWeight="SemiBold"
                           Foreground="#a6adc8" VerticalAlignment="Center" FontFamily="Segoe UI"/>
            </StackPanel>
        </Border>

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Center">
            <Button Name="btnStart"   Content="Start"   Width="110" Height="38" Margin="6,0" BorderThickness="0" Cursor="Hand" FontSize="13" FontWeight="Bold" FontFamily="Segoe UI" Foreground="#1e1e2e"><Button.Background><SolidColorBrush Color="#a6e3a1"/></Button.Background></Button>
            <Button Name="btnRestart" Content="Restart" Width="110" Height="38" Margin="6,0" BorderThickness="0" Cursor="Hand" FontSize="13" FontWeight="Bold" FontFamily="Segoe UI" Foreground="#1e1e2e"><Button.Background><SolidColorBrush Color="#89b4fa"/></Button.Background></Button>
            <Button Name="btnStop"    Content="Stop"    Width="110" Height="38" Margin="6,0" BorderThickness="0" Cursor="Hand" FontSize="13" FontWeight="Bold" FontFamily="Segoe UI" Foreground="#1e1e2e"><Button.Background><SolidColorBrush Color="#f38ba8"/></Button.Background></Button>
        </StackPanel>

        <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Bottom">
            <Button Name="btnOpenWeb" Content="Open Dashboard" Width="150" Height="32" Margin="6,0" BorderThickness="0" Cursor="Hand" FontSize="12" FontFamily="Segoe UI" Foreground="#cdd6f4"><Button.Background><SolidColorBrush Color="#45475a"/></Button.Background></Button>
            <Button Name="btnExit"    Content="Exit"           Width="110" Height="32" Margin="6,0" BorderThickness="0" Cursor="Hand" FontSize="12" FontFamily="Segoe UI" Foreground="#cdd6f4"><Button.Background><SolidColorBrush Color="#585b70"/></Button.Background></Button>
        </StackPanel>
    </Grid>
</Window>
"@

$reader = (New-Object System.Xml.XmlNodeReader $xaml)
$window = [Windows.Markup.XamlReader]::Load($reader)

$btnStart   = $window.FindName("btnStart")
$btnRestart = $window.FindName("btnRestart")
$btnStop    = $window.FindName("btnStop")
$btnExit    = $window.FindName("btnExit")
$btnOpenWeb = $window.FindName("btnOpenWeb")
$txtStatus  = $window.FindName("txtStatus")
$statusDot  = $window.FindName("statusDot")

function Update-Status {
    param([string]$Status, [string]$TextColor, [string]$DotColor)
    $txtStatus.Text       = $Status
    $txtStatus.Foreground = $TextColor
    $statusDot.Fill       = $DotColor
}

function Stop-Services {
    Update-Status "Stopping..." "#fab387" "#fab387"
    if ($global:watcherTimer) { $global:watcherTimer.Stop(); $global:watcherTimer = $null }
    if ($global:backendProcess  -and -not $global:backendProcess.HasExited)  { $global:backendProcess.Kill() }
    if ($global:frontendProcess -and -not $global:frontendProcess.HasExited) { $global:frontendProcess.Kill() }
    Get-CimInstance Win32_Process | Where-Object { $_.ProcessName -eq "ImagePilot.Api" } | Invoke-CimMethod -MethodName Terminate | Out-Null
    Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match "vite" -and $_.CommandLine -match "frontend" } | Invoke-CimMethod -MethodName Terminate | Out-Null
    $global:backendProcess  = $null
    $global:frontendProcess = $null
    Update-Status "Stopped" "#6c7086" "#6c7086"
    $btnStart.IsEnabled   = $true
    $btnStop.IsEnabled    = $false
    $btnRestart.IsEnabled = $false
}

function Start-Services {
    if ($global:backendProcess -ne $null -or $global:frontendProcess -ne $null) { return }
    Update-Status "Starting..." "#f9e2af" "#f9e2af"
    $btnStart.IsEnabled   = $false
    $btnStop.IsEnabled    = $true
    $btnRestart.IsEnabled = $true

    if (Test-Path $backendLog)  { Clear-Content $backendLog  -ErrorAction SilentlyContinue }
    if (Test-Path $frontendLog) { Clear-Content $frontendLog -ErrorAction SilentlyContinue }

    # Start Backend
    $bi = New-Object System.Diagnostics.ProcessStartInfo
    $bi.FileName         = "cmd.exe"
    $bi.Arguments        = "/c `"dotnet run --project `"$root\backend\ImagePilot.Api.csproj`" > `"$backendLog`" 2>&1`""
    $bi.WorkingDirectory = $root
    $bi.WindowStyle      = "Hidden"; $bi.CreateNoWindow = $true
    $global:backendProcess = [System.Diagnostics.Process]::Start($bi)

    # Start Frontend
    $fi = New-Object System.Diagnostics.ProcessStartInfo
    $fi.FileName         = "cmd.exe"
    $fi.Arguments        = "/c `"npm.cmd run dev -- --host localhost > `"$frontendLog`" 2>&1`""
    $fi.WorkingDirectory = "$root\frontend"
    $fi.WindowStyle      = "Hidden"; $fi.CreateNoWindow = $true
    $global:frontendProcess = [System.Diagnostics.Process]::Start($fi)

    # Poll until Vite is ready, then open browser automatically
    $global:watcherTimer = New-Object System.Windows.Threading.DispatcherTimer
    $global:watcherTimer.Interval = [TimeSpan]::FromSeconds(2)
    $global:watcherTimer.Add_Tick({
        if (Test-Path $frontendLog) {
            $content = Get-Content $frontendLog -Raw -ErrorAction SilentlyContinue
            if ($content -match "Local:" -or $content -match "localhost:5173") {
                $global:watcherTimer.Stop()
                $global:watcherTimer = $null
                Update-Status "Running" "#a6e3a1" "#a6e3a1"
                Start-Process "http://localhost:5173"
            }
        }
    })
    $global:watcherTimer.Start()
}

$btnStart.Add_Click({   Start-Services })
$btnStop.Add_Click({    Stop-Services  })
$btnRestart.Add_Click({
    Stop-Services
    Start-Sleep -Seconds 2
    Start-Services
})
$btnOpenWeb.Add_Click({ Start-Process "http://localhost:5173" })
$btnExit.Add_Click({    Stop-Services; $window.Close() })
$window.Add_Closed({    Stop-Services })

# Initial UI state
Update-Status "Stopped" "#6c7086" "#6c7086"
$btnStop.IsEnabled    = $false
$btnRestart.IsEnabled = $false

# Auto-start when the window first appears
$window.Add_ContentRendered({ Start-Services })

$window.ShowDialog() | Out-Null
