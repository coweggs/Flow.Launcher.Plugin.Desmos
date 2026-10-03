$projectPath = Join-Path $PSScriptRoot "Flow.Launcher.Plugin.Desmos\Flow.Launcher.Plugin.Desmos.csproj"
$publishPath = Join-Path $PSScriptRoot "Flow.Launcher.Plugin.Desmos\bin\Debug\win-x64\publish"

dotnet publish $projectPath -c Debug -r win-x64 --no-self-contained

if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

$appDataFolder = [Environment]::GetFolderPath("ApplicationData")
$flowLauncherExe = Join-Path $env:LOCALAPPDATA "FlowLauncher\Flow.Launcher.exe"
$pluginPath = Join-Path $appDataFolder "FlowLauncher\Plugins\Desmos"

if (-not (Test-Path $flowLauncherExe)) {
    throw "Flow.Launcher.exe not found at $flowLauncherExe"
}

Stop-Process -Name "Flow.Launcher" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

if (Test-Path $pluginPath) {
    Remove-Item $pluginPath -Recurse -Force
}

New-Item -ItemType Directory -Path $pluginPath -Force | Out-Null
Copy-Item (Join-Path $publishPath "*") $pluginPath -Recurse -Force

Start-Process $flowLauncherExe