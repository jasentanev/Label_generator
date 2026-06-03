param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$distRoot = Join-Path $root "dist"
$publishDir = Join-Path $distRoot "LabelGenerator"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$zipPath = Join-Path $distRoot "LabelGenerator-$Runtime-$timestamp.zip"

Write-Host "Label Generator build"
Write-Host "Root: $root"
Write-Host "Configuration: $Configuration"
Write-Host "Runtime: $Runtime"
Write-Host "Output: $publishDir"
Write-Host "Zip: $zipPath"

if (-not $SkipTests) {
    Write-Host ""
    Write-Host "Running tests..."
    dotnet test (Join-Path $root "LabelGenerator.slnx") --no-restore
}

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

$resolvedDistRoot = (Resolve-Path $distRoot).Path
if (Test-Path $publishDir) {
    $resolvedPublishDir = (Resolve-Path $publishDir).Path
    if (-not $resolvedPublishDir.StartsWith($resolvedDistRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to delete unexpected publish directory: $resolvedPublishDir"
    }

    Write-Host ""
    Write-Host "Cleaning publish directory..."
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

Write-Host ""
Write-Host "Publishing main app..."
dotnet publish (Join-Path $root "LabelGenerator.App\LabelGenerator.App.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir

Write-Host ""
Write-Host "Publishing designer app..."
dotnet publish (Join-Path $root "LabelGenerator.Designer\LabelGenerator.Designer.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir

Write-Host ""
Write-Host "Publishing starter app..."
dotnet publish (Join-Path $root "LabelGenerator.Starter\LabelGenerator.Starter.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir

Write-Host ""
Write-Host "Creating timestamped zip..."
Compress-Archive -Path $publishDir -DestinationPath $zipPath -Force

$latestZipPath = Join-Path $distRoot "LabelGenerator-$Runtime-latest.zip"
Copy-Item -LiteralPath $zipPath -Destination $latestZipPath -Force

Write-Host ""
Write-Host "Build complete."
Write-Host "Package: $zipPath"
Write-Host "Latest copy: $latestZipPath"
