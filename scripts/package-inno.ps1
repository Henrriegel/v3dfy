[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$installerScript = Join-Path $repoRoot 'packaging\inno\v3dfy.iss'
$compilerCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ }

if (-not (Test-Path -LiteralPath $installerScript)) {
    Write-Warning "Inno Setup script is missing: $installerScript"
    return
}

$compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $compiler) {
    $compiler = $compilerCandidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

if (-not $compiler) {
    Write-Warning 'Inno Setup Compiler was not found. Install Inno Setup before packaging.'
    return
}

$compilerPath = if ($compiler.Source) { $compiler.Source } else { $compiler }
& $compilerPath $installerScript
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Inno Setup Compiler exited with code $LASTEXITCODE."
    return
}

Write-Host 'Inno Setup packaging completed.' -ForegroundColor Green
