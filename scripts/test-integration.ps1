[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Warning "This runs integration tests."
Write-Warning "These tests may start local processes."
Write-Warning "They are not part of the default safe test script."

Push-Location $repoRoot
try {
    & dotnet test .\v3dfy.slnx --filter "Category=Integration"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
