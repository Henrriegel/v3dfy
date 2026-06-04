[CmdletBinding()]
param(
    [string]$Iw3BundleRoot,
    [switch]$IncludeIw3Bundle,
    [switch]$StrictModels,
    [switch]$RequireCapabilities
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $repoRoot 'artifacts\publish\v3dfy-win-x64'
$appProject = Join-Path $repoRoot 'src\V3dfy.App\V3dfy.App.csproj'
$stageIw3BundleScript = Join-Path $PSScriptRoot 'stage-iw3-bundle.ps1'
$shouldStageIw3Bundle = $IncludeIw3Bundle.IsPresent -or
    -not [string]::IsNullOrWhiteSpace($Iw3BundleRoot)

function Write-PublishMessage {
    param(
        [ValidateSet('OK', 'WARN', 'FAIL')]
        [string]$Level,
        [string]$Message
    )

    $color = switch ($Level) {
        'OK' { 'Green' }
        'WARN' { 'Yellow' }
        'FAIL' { 'Red' }
    }

    Write-Host "[$Level] $Message" -ForegroundColor $color
}

function Resolve-FullPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Copy-RepositoryDirectory {
    param([string]$DirectoryName)

    $source = Join-Path $repoRoot $DirectoryName
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        Write-PublishMessage WARN "Repository directory not found; skipping copy: $source"
        return
    }

    Copy-Item -LiteralPath $source -Destination $publishDirectory -Recurse -Force
    Write-PublishMessage OK "Copied repository directory into publish output: $DirectoryName"
}

function Get-Iw3BundleSource {
    if (-not [string]::IsNullOrWhiteSpace($Iw3BundleRoot)) {
        return Resolve-FullPath $Iw3BundleRoot
    }

    return Join-Path $repoRoot 'engine\iw3'
}

function Invoke-Iw3BundleStaging {
    $sourceBundleRoot = Get-Iw3BundleSource
    $targetBundleRoot = Join-Path $publishDirectory 'engine\iw3'

    if (-not (Test-Path -LiteralPath $stageIw3BundleScript -PathType Leaf)) {
        Write-PublishMessage FAIL "iw3 staging script is missing: $stageIw3BundleScript"
        throw "iw3 staging script is missing: $stageIw3BundleScript"
    }

    Write-PublishMessage OK 'iw3 bundle staging requested.'
    Write-Host "Source iw3 bundle: $sourceBundleRoot"
    Write-Host "Publish target root: $publishDirectory"
    Write-Host "Target iw3 bundle: $targetBundleRoot"

    $stageArguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $stageIw3BundleScript,
        '-SourceBundleRoot',
        $sourceBundleRoot,
        '-TargetRoot',
        $publishDirectory,
        '-CleanTarget'
    )

    if ($StrictModels.IsPresent) {
        $stageArguments += '-StrictModels'
    }

    if ($RequireCapabilities.IsPresent) {
        $stageArguments += '-RequireCapabilities'
    }

    & powershell @stageArguments
    if ($LASTEXITCODE -ne 0) {
        Write-PublishMessage FAIL "iw3 bundle staging failed with exit code $LASTEXITCODE."
        throw "iw3 bundle staging failed with exit code $LASTEXITCODE."
    }

    Write-PublishMessage OK 'iw3 bundle staging completed.'
}

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
foreach ($directoryName in @('tools', 'licenses')) {
    Copy-RepositoryDirectory $directoryName
}

if ($shouldStageIw3Bundle) {
    Write-PublishMessage WARN 'Skipping broad repository engine copy because explicit iw3 bundle staging was requested.'
    Invoke-Iw3BundleStaging
}
else {
    Copy-RepositoryDirectory 'engine'
    Write-PublishMessage WARN 'No iw3 bundle staging requested. Use -Iw3BundleRoot or -IncludeIw3Bundle to validate and stage engine\iw3 explicitly.'
}

Write-PublishMessage OK "Published Windows x64 bundle: $publishDirectory"
