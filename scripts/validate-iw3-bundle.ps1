[CmdletBinding()]
param(
    [string]$BundleRoot,
    [switch]$StrictModels,
    [switch]$RequireCapabilities
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$script:failureCount = 0

$supportedModelExtensions = @(
    '.pth',
    '.pt',
    '.onnx',
    '.safetensors',
    '.ckpt',
    '.bin'
)
$placeholderOrContractFileNames = @(
    'README.md',
    'ENGINE_MANIFEST.json',
    'ENGINE_BUNDLE_CONTRACT.md',
    'IW3_CLI_CAPABILITIES.json',
    'MODEL_CATALOG.json'
)

function Write-Check {
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

function Write-Failure {
    param([string]$Message)

    $script:failureCount++
    Write-Check FAIL $Message
}

function Write-WarningOrFailure {
    param(
        [bool]$Fail,
        [string]$Message
    )

    if ($Fail) {
        Write-Failure $Message
    }
    else {
        Write-Check WARN $Message
    }
}

function Get-FullPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'engine\iw3'))
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Get-JsonPropertyValue {
    param(
        [object]$JsonObject,
        [string]$Name
    )

    if ($null -eq $JsonObject) {
        return $null
    }

    $property = $JsonObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-JsonString {
    param(
        [object]$JsonObject,
        [string]$Name
    )

    $value = Get-JsonPropertyValue $JsonObject $Name
    if ($null -eq $value) {
        return ''
    }

    return [string]$value
}

function Test-PlaceholderManifest {
    param([object]$JsonObject)

    $placeholder = Get-JsonPropertyValue $JsonObject 'placeholder'
    if ($placeholder -eq $true) {
        return $true
    }

    foreach ($name in @('version', 'iw3Version', 'bundledIw3Version')) {
        $value = Get-JsonString $JsonObject $name
        if ($value -and $value -eq 'placeholder') {
            return $true
        }
    }

    return $false
}

function Test-JsonStringArrayProperty {
    param(
        [object]$JsonObject,
        [string]$Name,
        [ref]$ErrorMessage
    )

    $value = Get-JsonPropertyValue $JsonObject $Name
    if ($null -eq $value) {
        return $true
    }

    if ($value -is [string]) {
        $ErrorMessage.Value = "$Name must be an array of strings."
        return $false
    }

    if ($value -isnot [System.Array]) {
        $ErrorMessage.Value = "$Name must be an array of strings."
        return $false
    }

    foreach ($entry in $value) {
        if ($entry -isnot [string]) {
            $ErrorMessage.Value = "$Name entries must be strings."
            return $false
        }
    }

    return $true
}

function Get-JsonArrayCount {
    param(
        [object]$JsonObject,
        [string]$Name
    )

    $value = Get-JsonPropertyValue $JsonObject $Name
    if ($null -eq $value) {
        return 0
    }

    if ($value -is [System.Array]) {
        return $value.Count
    }

    return 1
}

function Read-JsonFile {
    param(
        [string]$Path,
        [string]$Description
    )

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        Write-Failure "$Description is not valid JSON: $Path"
        return $null
    }
}

function Test-RequiredFile {
    param(
        [string]$Path,
        [string]$Description
    )

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Write-Check OK "$Description found: $Path"
        return $true
    }

    Write-Failure "$Description is missing: $Path"
    return $false
}

function Test-EngineManifest {
    param([string]$EngineRoot)

    $manifestPath = Join-Path $EngineRoot 'ENGINE_MANIFEST.json'
    if (-not (Test-RequiredFile $manifestPath 'ENGINE_MANIFEST.json')) {
        return
    }

    $manifest = Read-JsonFile $manifestPath 'ENGINE_MANIFEST.json'
    if ($null -eq $manifest) {
        return
    }

    $version = Get-JsonString $manifest 'version'
    if ([string]::IsNullOrWhiteSpace($version)) {
        Write-Failure "ENGINE_MANIFEST.json version is missing: $manifestPath"
        return
    }

    if ($version -eq 'placeholder') {
        Write-Failure "ENGINE_MANIFEST.json version is placeholder: $manifestPath"
        return
    }

    Write-Check OK "ENGINE_MANIFEST.json version is set: $version"
}

function Test-EngineEntry {
    param([string]$EngineRoot)

    $packageEntry = Join-Path $EngineRoot 'nunif\iw3\__main__.py'

    if (Test-Path -LiteralPath $packageEntry -PathType Leaf) {
        Write-Check OK "iw3 package entry file found: $packageEntry"
        return
    }

    Write-Failure "iw3 package entry file is missing. Expected nunif\iw3\__main__.py under: $EngineRoot"
}

function Test-ModelFile {
    param([System.IO.FileInfo]$File)

    if ($File.Name -eq '.gitkeep') {
        return $false
    }

    if ($File.Name -in $placeholderOrContractFileNames) {
        return $false
    }

    if ($File.BaseName -eq 'placeholder') {
        return $false
    }

    return $File.Extension.ToLowerInvariant() -in $supportedModelExtensions
}

function Test-ModelsDirectory {
    param([string]$EngineRoot)

    $modelsPath = Join-Path $EngineRoot 'nunif\iw3\pretrained_models'
    if (-not (Test-Path -LiteralPath $modelsPath -PathType Container)) {
        Write-Failure "pretrained models directory is missing: $modelsPath"
        return
    }

    Write-Check OK "pretrained models directory found: $modelsPath"

    $modelFiles = @(Get-ChildItem -LiteralPath $modelsPath -File -Recurse |
        Where-Object { Test-ModelFile $_ })

    if ($modelFiles.Count -gt 0) {
        Write-Check OK "compatible model files found: $($modelFiles.Count)"
        return
    }

    Write-WarningOrFailure `
        -Fail:$StrictModels.IsPresent `
        -Message "no compatible model files found under: $modelsPath"
}

function Test-Iw3RuntimeDependencies {
    param([string]$EngineRoot)

    $dependencyPath = Join-Path $EngineRoot 'nunif\iw3\pretrained_models\hub\checkpoints\iw3_row_flow_v3_20250627.pth'
    Test-RequiredFile $dependencyPath 'iw3 row_flow_v3 runtime dependency' | Out-Null
}

function Test-CliCapabilities {
    param([string]$EngineRoot)

    $capabilitiesPath = Join-Path $EngineRoot 'IW3_CLI_CAPABILITIES.json'
    if (-not (Test-Path -LiteralPath $capabilitiesPath -PathType Leaf)) {
        Write-WarningOrFailure `
            -Fail:$RequireCapabilities.IsPresent `
            -Message "optional IW3_CLI_CAPABILITIES.json is missing. v3dfy must use the base iw3 command only: $capabilitiesPath"
        return
    }

    $capabilities = $null
    try {
        $capabilities = Get-Content -LiteralPath $capabilitiesPath -Raw | ConvertFrom-Json
    }
    catch {
        Write-WarningOrFailure `
            -Fail:$RequireCapabilities.IsPresent `
            -Message "IW3_CLI_CAPABILITIES.json is not valid JSON: $capabilitiesPath"
        return
    }

    if ($null -eq $capabilities -or $capabilities -is [System.Array]) {
        Write-WarningOrFailure `
            -Fail:$RequireCapabilities.IsPresent `
            -Message "IW3_CLI_CAPABILITIES.json root must be a JSON object: $capabilitiesPath"
        return
    }

    if (Test-PlaceholderManifest $capabilities) {
        Write-WarningOrFailure `
            -Fail:$RequireCapabilities.IsPresent `
            -Message "IW3_CLI_CAPABILITIES.json is a placeholder and does not verify CLI options: $capabilitiesPath"
        return
    }

    $arrayError = ''
    if (-not (Test-JsonStringArrayProperty $capabilities 'verifiedOptions' ([ref]$arrayError)) -or
        -not (Test-JsonStringArrayProperty $capabilities 'unverifiedOptions' ([ref]$arrayError))) {
        Write-WarningOrFailure `
            -Fail:$RequireCapabilities.IsPresent `
            -Message "IW3_CLI_CAPABILITIES.json has invalid option metadata. $arrayError"
        return
    }

    $bundledVersion = Get-JsonString $capabilities 'bundledIw3Version'
    if ([string]::IsNullOrWhiteSpace($bundledVersion)) {
        $bundledVersion = Get-JsonString $capabilities 'iw3Version'
    }

    if ([string]::IsNullOrWhiteSpace($bundledVersion)) {
        Write-WarningOrFailure `
            -Fail:$RequireCapabilities.IsPresent `
            -Message "IW3_CLI_CAPABILITIES.json does not record bundledIw3Version or iw3Version."
    }
    else {
        Write-Check OK "IW3_CLI_CAPABILITIES.json bundled iw3 version: $bundledVersion"
    }

    $verifiedBaseCommand = Get-JsonPropertyValue $capabilities 'verifiedBaseCommand'
    if ($verifiedBaseCommand -eq $true) {
        Write-Check OK 'IW3_CLI_CAPABILITIES.json verifies the base command.'
    }
    else {
        Write-WarningOrFailure `
            -Fail:$RequireCapabilities.IsPresent `
            -Message 'IW3_CLI_CAPABILITIES.json does not mark verifiedBaseCommand=true.'
    }

    Write-Check OK "IW3_CLI_CAPABILITIES.json verified option count: $(Get-JsonArrayCount $capabilities 'verifiedOptions')"
    Write-Check WARN 'Unverified iw3 options must remain disabled until this manifest records verification for the bundled iw3 version.'
}

$bundlePath = Get-FullPath $BundleRoot
Write-Host "Validating iw3 bundle layout: $bundlePath"
Write-Host 'This script inspects files only. It does not run Python, iw3, FFmpeg, model loading, downloads, or installs.'

if (-not (Test-Path -LiteralPath $bundlePath -PathType Container)) {
    Write-Failure "iw3 bundle root is missing: $bundlePath"
}
else {
    Write-Check OK "iw3 bundle root found: $bundlePath"
    Test-EngineManifest $bundlePath
    Test-RequiredFile (Join-Path $bundlePath 'python\python.exe') 'embedded Python executable' | Out-Null
    Test-RequiredFile (Join-Path $bundlePath 'python\python312._pth') 'embedded Python path file' | Out-Null
    Test-EngineEntry $bundlePath
    Test-ModelsDirectory $bundlePath
    Test-Iw3RuntimeDependencies $bundlePath
    Test-CliCapabilities $bundlePath
}

if ($script:failureCount -gt 0) {
    Write-Check FAIL "iw3 bundle layout validation failed with $script:failureCount required issue(s)."
    exit 1
}

Write-Check OK 'iw3 bundle layout validation completed.'
