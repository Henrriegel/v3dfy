[CmdletBinding()]
param(
    [string]$Version = '0.1.0-preview.1',
    [string]$PublishDir = 'artifacts\publish\v3dfy-win-x64',
    [string]$ReleaseDir = 'artifacts\release',
    [string]$SplitDir = 'artifacts\release\split',
    [int]$PartCount = 3
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$portableZipFileName = "v3dfy-v$Version-win-x64-portable.zip"
$basePayloadModelValidatorScript = Join-Path $PSScriptRoot 'validate-base-payload-models.ps1'

function Write-PayloadMessage {
    param(
        [ValidateSet('OK', 'WARN', 'FAIL', 'INFO')]
        [string]$Level,
        [string]$Message
    )

    $color = switch ($Level) {
        'OK' { 'Green' }
        'WARN' { 'Yellow' }
        'FAIL' { 'Red' }
        'INFO' { 'Cyan' }
    }

    Write-Host "[$Level] $Message" -ForegroundColor $color
}

function Resolve-RepositoryPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
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

function Get-RequiredDirectory {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Description is missing: $Path"
    }

    return Get-Item -LiteralPath $Path
}

function Get-RequiredFile {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description is missing: $Path"
    }

    return Get-Item -LiteralPath $Path
}

function Invoke-BasePayloadModelValidation {
    param([string]$PublishDirectory)

    Get-RequiredFile $basePayloadModelValidatorScript 'Base payload model validator' | Out-Null

    $validationOutput = & powershell `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $basePayloadModelValidatorScript `
        -PublishDir $PublishDirectory 2>&1
    foreach ($line in $validationOutput) {
        Write-Host $line
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Base payload model validation failed with exit code $LASTEXITCODE."
    }
}

function Split-PortableZip {
    param(
        [string]$ZipPath,
        [string]$TargetDirectory,
        [string]$ZipFileName,
        [int]$Count
    )

    if ($Count -ne 3) {
        throw 'Release payload split count must remain 3 for the current installer/release asset contract.'
    }

    $zipFile = Get-RequiredFile $ZipPath 'Portable ZIP'
    if ($zipFile.Length -lt $Count) {
        throw "Portable ZIP is too small to split into $Count non-empty parts: $ZipPath"
    }

    New-Item -ItemType Directory -Force -Path $TargetDirectory | Out-Null
    Get-ChildItem -LiteralPath $TargetDirectory -Filter "$ZipFileName.part*" -File |
        Remove-Item -Force

    $partSize = [int64][Math]::Ceiling($zipFile.Length / [double]$Count)
    $buffer = New-Object byte[] 1048576
    $parts = @()

    $source = [System.IO.File]::OpenRead($ZipPath)
    try {
        for ($index = 1; $index -le $Count; $index++) {
            $partName = '{0}.part{1:D2}' -f $ZipFileName, $index
            $partPath = Join-Path $TargetDirectory $partName
            $remainingForPart = if ($index -lt $Count) {
                [Math]::Min($partSize, $zipFile.Length - $source.Position)
            }
            else {
                $zipFile.Length - $source.Position
            }

            if ($remainingForPart -le 0) {
                throw "Portable ZIP did not have enough bytes for non-empty part $index."
            }

            $destination = [System.IO.File]::Create($partPath)
            try {
                while ($remainingForPart -gt 0) {
                    $readLength = [int][Math]::Min([int64]$buffer.Length, $remainingForPart)
                    $bytesRead = $source.Read($buffer, 0, $readLength)
                    if ($bytesRead -le 0) {
                        throw "Unexpected end of ZIP while writing $partName."
                    }

                    $destination.Write($buffer, 0, $bytesRead)
                    $remainingForPart -= $bytesRead
                }
            }
            finally {
                $destination.Dispose()
            }

            $parts += Get-Item -LiteralPath $partPath
            Write-PayloadMessage OK "Wrote payload part: $partPath"
        }
    }
    finally {
        $source.Dispose()
    }

    return @($parts)
}

function Write-ReleasePayloadChecksums {
    param(
        [string]$ZipPath,
        [System.IO.FileInfo[]]$PartFiles,
        [string]$OutputPath
    )

    $files = @((Get-RequiredFile $ZipPath 'Portable ZIP')) + @($PartFiles)
    $lines = foreach ($file in $files) {
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        "$hash  $($file.Name)"
    }

    $lines | Set-Content -LiteralPath $OutputPath -Encoding ASCII
    Write-PayloadMessage OK "Generated release payload checksums: $OutputPath"
}

if ($PartCount -ne 3) {
    throw 'Release payload split count must remain 3 for the current installer/release asset contract.'
}

$publishDirectory = Resolve-RepositoryPath $PublishDir
$releaseDirectory = Resolve-RepositoryPath $ReleaseDir
$splitDirectory = Resolve-RepositoryPath $SplitDir
Assert-RepositoryChildPath $releaseDirectory 'Release payload output directory'
Assert-RepositoryChildPath $splitDirectory 'Release payload split directory'

Get-RequiredDirectory $publishDirectory 'Windows x64 publish output' | Out-Null
Get-RequiredFile (Join-Path $publishDirectory 'V3dfy.App.exe') 'Published app executable' | Out-Null
Get-RequiredFile (Join-Path $publishDirectory 'V3dfy.SetupHelper.exe') 'Published setup helper executable' | Out-Null
Invoke-BasePayloadModelValidation $publishDirectory

New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
$zipPath = Join-Path $releaseDirectory $portableZipFileName
if (Test-Path -LiteralPath $zipPath -PathType Leaf) {
    Remove-Item -LiteralPath $zipPath -Force
}

Write-PayloadMessage INFO "Creating release portable ZIP from publish output: $publishDirectory"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $publishDirectory,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)
Write-PayloadMessage OK "Created release portable ZIP: $zipPath"

$partFiles = Split-PortableZip `
    -ZipPath $zipPath `
    -TargetDirectory $splitDirectory `
    -ZipFileName $portableZipFileName `
    -Count $PartCount

Write-ReleasePayloadChecksums `
    -ZipPath $zipPath `
    -PartFiles $partFiles `
    -OutputPath (Join-Path $splitDirectory 'SHA256SUMS.txt')

$zipSizeMiB = [Math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 2)
Write-PayloadMessage OK "${portableZipFileName}: $zipSizeMiB MiB"
Write-PayloadMessage OK 'Release payload packaging completed.'
