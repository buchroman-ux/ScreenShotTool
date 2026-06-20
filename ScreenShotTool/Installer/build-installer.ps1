param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "ScreenShotTool.csproj"
$repoRoot = Split-Path -Parent $projectRoot
$installerProject = Join-Path $repoRoot "ScreenShotTool.Installer\ScreenShotTool.Installer.csproj"
$payloadDir = Join-Path $repoRoot "ScreenShotTool.Installer\Payload"
$payloadZip = Join-Path $payloadDir "ScreenShotToolPayload.zip"
$publishDir = Join-Path $projectRoot "bin\$Configuration\net8.0-windows\$Runtime\publish"
$installerOutput = Join-Path $projectRoot "artifacts\installer"

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
if (Test-Path $installerOutput) {
    Remove-Item -LiteralPath $installerOutput -Recurse -Force
}

dotnet publish $projectFile -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=false

New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null
if (Test-Path $payloadZip) {
    Remove-Item -LiteralPath $payloadZip -Force
}
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $payloadZip -Force

dotnet publish $installerProject -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $installerOutput
Move-Item -LiteralPath (Join-Path $installerOutput "ScreenShotToolInstaller.exe") -Destination (Join-Path $installerOutput "ScreenShotToolSetup.exe") -Force

$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if (-not $iscc) {
    Write-Warning "Inno Setup compiler ISCC.exe was not found on PATH. The custom installer was created, but the optional Inno installer was skipped."
    exit 0
}

& $iscc.Source (Join-Path $PSScriptRoot "ScreenShotTool.iss")
