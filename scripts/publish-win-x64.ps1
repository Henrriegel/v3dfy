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
$setupHelperProject = Join-Path $repoRoot 'src\V3dfy.SetupHelper\V3dfy.SetupHelper.csproj'
$setupHelperPublishDirectory = Join-Path $repoRoot 'artifacts\publish\v3dfy-setup-helper-win-x64'
$stageIw3BundleScript = Join-Path $PSScriptRoot 'stage-iw3-bundle.ps1'
$basePayloadModelValidatorScript = Join-Path $PSScriptRoot 'validate-base-payload-models.ps1'
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

function Assert-RepositoryChildPath {
    param(
        [string]$Path,
        [string]$Description
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootWithSeparator = if ($repoRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $repoRoot
    }
    else {
        $repoRoot + [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $fullPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must stay inside the repository: $fullPath"
    }
}

function Test-SameOrNestedPath {
    param(
        [string]$FirstPath,
        [string]$SecondPath
    )

    $first = [System.IO.Path]::GetFullPath($FirstPath).TrimEnd('\', '/')
    $second = [System.IO.Path]::GetFullPath($SecondPath).TrimEnd('\', '/')

    if ($first.Equals($second, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    return $first.StartsWith(
            "$second\",
            [System.StringComparison]::OrdinalIgnoreCase) -or
        $second.StartsWith(
            "$first\",
            [System.StringComparison]::OrdinalIgnoreCase)
}

function Reset-Directory {
    param(
        [string]$Path,
        [string]$Description
    )

    Assert-RepositoryChildPath $Path $Description
    if (Test-Path -LiteralPath $Path -PathType Container) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    Write-PublishMessage OK "Cleaned $Description`: $Path"
}

function Copy-RepositoryDirectory {
    param([string]$DirectoryName)

    $source = Join-Path $repoRoot $DirectoryName
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        Write-PublishMessage WARN "Repository directory not found; skipping copy: $source"
        return
    }

    $target = Join-Path $publishDirectory $DirectoryName
    Assert-RepositoryChildPath $target "Publish target for $DirectoryName"
    if (Test-Path -LiteralPath $target -PathType Container) {
        Remove-Item -LiteralPath $target -Recurse -Force
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

function Invoke-BasePayloadModelValidation {
    if (-not (Test-Path -LiteralPath $basePayloadModelValidatorScript -PathType Leaf)) {
        Write-PublishMessage FAIL "base payload model validator is missing: $basePayloadModelValidatorScript"
        throw "base payload model validator is missing: $basePayloadModelValidatorScript"
    }

    $validationOutput = & powershell `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $basePayloadModelValidatorScript `
        -PublishDir $publishDirectory 2>&1
    foreach ($line in $validationOutput) {
        Write-Host $line
    }

    if ($LASTEXITCODE -ne 0) {
        Write-PublishMessage FAIL "base payload model validation failed with exit code $LASTEXITCODE."
        throw "base payload model validation failed with exit code $LASTEXITCODE."
    }
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

function Publish-SetupHelperForApp {
    if (Test-Path -LiteralPath $setupHelperPublishDirectory -PathType Container) {
        Remove-Item -LiteralPath $setupHelperPublishDirectory -Recurse -Force
    }

    Write-PublishMessage OK 'Publishing setup helper for app model-pack imports.'
    & dotnet publish $setupHelperProject `
        --configuration Release `
        --framework net10.0-windows `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        --output $setupHelperPublishDirectory

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish for setup helper failed with exit code $LASTEXITCODE."
    }

    $helperExe = Join-Path $setupHelperPublishDirectory 'V3dfy.SetupHelper.exe'
    if (-not (Test-Path -LiteralPath $helperExe -PathType Leaf)) {
        throw "Published setup helper was not found: $helperExe"
    }

    Get-ChildItem -LiteralPath $setupHelperPublishDirectory -Force |
        Copy-Item -Destination $publishDirectory -Recurse -Force

    Write-PublishMessage OK 'Copied setup helper into app publish output: V3dfy.SetupHelper.exe'
}

if ($shouldStageIw3Bundle) {
    $sourceBundleRoot = Get-Iw3BundleSource
    if (Test-SameOrNestedPath $sourceBundleRoot $publishDirectory) {
        throw "Iw3BundleRoot must not be the publish output or a child of it because publish output is cleaned before publishing: $sourceBundleRoot"
    }
}

Reset-Directory $publishDirectory 'Windows x64 publish output directory'

& dotnet publish $appProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Publish-SetupHelperForApp

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

Invoke-BasePayloadModelValidation

Write-PublishMessage OK "Published Windows x64 bundle: $publishDirectory"
