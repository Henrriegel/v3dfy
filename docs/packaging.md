# Packaging

## Publish target

v3dfy targets a self-contained Windows x64 publish:

```powershell
.\scripts\publish-win-x64.ps1
```

The script publishes `src/V3dfy.App/V3dfy.App.csproj` to
`artifacts/publish/v3dfy-win-x64`. A normal publish does not force iw3 bundle
validation or staging:

```powershell
.\scripts\publish-win-x64.ps1
```

The default path remains stable and the script preserves the current
repository support-folder copy behavior for `tools`, `engine`, and `licenses`
when those directories exist. Prefer the explicit staging options below for a
real iw3 bundle so the bundle is validated before it is copied.

The expected local engine bundle contract is documented in `docs/engine.md`.
Packaging must preserve that inspectable layout, including
`engine/iw3/ENGINE_MANIFEST.json`, `engine/iw3/python/python.exe`, an iw3 entry
file at `engine/iw3/iw3.py` or `engine/iw3/iw3/__main__.py`, and
`engine/iw3/models`.

Before packaging a local iw3 bundle, validate its structure without running the
engine:

```powershell
.\scripts\validate-iw3-bundle.ps1
.\scripts\validate-iw3-bundle.ps1 -StrictModels -RequireCapabilities
```

After validation, stage the bundle into an app or publish layout:

```powershell
.\scripts\stage-iw3-bundle.ps1 -TargetRoot artifacts\publish\v3dfy-win-x64
```

The publish script can also validate and stage an iw3 bundle as part of the
publish flow:

```powershell
.\scripts\publish-win-x64.ps1 -Iw3BundleRoot C:\path\to\iw3-bundle
.\scripts\publish-win-x64.ps1 -Iw3BundleRoot C:\path\to\iw3-bundle -StrictModels -RequireCapabilities
.\scripts\publish-win-x64.ps1 -IncludeIw3Bundle
```

`-Iw3BundleRoot` stages the supplied bundle. `-IncludeIw3Bundle` uses the
repo-local `engine\iw3` bundle when no bundle root is supplied. When either
option is used, publish skips the broad post-publish `engine` copy and calls
`scripts/stage-iw3-bundle.ps1` with the publish directory as the target. The
staged runtime layout is always:

```text
artifacts/publish/v3dfy-win-x64/engine/iw3
```

Validation runs before staging. If validation fails, staging fails and the iw3
bundle is not copied by the staging script. The validation and staging scripts
inspect and copy local files only. They do not run Python, iw3, FFmpeg, model
loading, downloads, or installs.

## Why PublishSingleFile is disabled

`PublishSingleFile` is intentionally not enabled by default. The local engine,
embedded runtime, FFmpeg tools, models, configuration, and license files need a
clear inspectable directory layout. Keeping these components separate also
simplifies diagnostics and future updates.

## Installer

The initial installer target is Inno Setup. Run packaging preflight without
compiling the installer with:

```powershell
.\scripts\package-inno.ps1 -PreflightOnly
.\scripts\package-inno.ps1 -RequireIw3Bundle -PreflightOnly
.\scripts\package-inno.ps1 -RequireIw3Bundle -StrictModels -RequireCapabilities -PreflightOnly
```

Normal installer preflight checks the publish output:

- `artifacts\publish\v3dfy-win-x64` exists.
- `V3dfy.App.exe` exists.
- `tools\ffmpeg\win-x64\ffmpeg.exe` exists.
- `tools\ffmpeg\win-x64\ffprobe.exe` exists.

For a full offline installer that must include iw3, require the staged bundle
preflight. With `-RequireIw3Bundle`, packaging also checks
`artifacts\publish\v3dfy-win-x64\engine\iw3` and calls
`scripts\validate-iw3-bundle.ps1` against that published bundle. `-StrictModels`
and `-RequireCapabilities` are passed through to the bundle validator.

After preflight passes, build the installer with:

```powershell
.\scripts\package-inno.ps1
.\scripts\package-inno.ps1 -RequireIw3Bundle
.\scripts\package-inno.ps1 -RequireIw3Bundle -StrictModels -RequireCapabilities
```

If preflight fails, Inno packaging is skipped. With `-PreflightOnly`, Inno
packaging is skipped after successful preflight. Packaging preflight inspects
files only. It does not run the app, Python, iw3, FFmpeg, model loading,
downloads, or installs.

Before release, verify that the published app works on a clean Windows x64
machine without global .NET, Python, FFmpeg, models, or development tools.
Run installer packaging only after the publish output contains the expected
`engine\iw3` layout for the bundle you intend to ship.
