[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $repoRoot 'artifacts\publish\v3dfy-win-x64'
$appProject = Join-Path $repoRoot 'src\V3dfy.App\V3dfy.App.csproj'

& dotnet publish $appProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# The final installer must include these folders next to the published app.
# They contain offline tools, the local engine, models, runtime, and licenses.
foreach ($directoryName in @('tools', 'engine', 'licenses')) {
    $source = Join-Path $repoRoot $directoryName
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination $publishDirectory -Recurse -Force
    }
}

Write-Host "Published Windows x64 bundle: $publishDirectory" -ForegroundColor Green
