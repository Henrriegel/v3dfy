# Packaging

## Publish target

v3dfy targets a self-contained Windows x64 publish:

```powershell
.\scripts\publish-win-x64.ps1
```

The script publishes `src/V3dfy.App/V3dfy.App.csproj` to
`artifacts/publish/v3dfy-win-x64` and copies `tools`, `engine`, and `licenses`
next to the app when those directories exist.

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

The validator checks local files only. It does not run Python, iw3, FFmpeg,
model loading, downloads, or installs.

## Why PublishSingleFile is disabled

`PublishSingleFile` is intentionally not enabled by default. The local engine,
embedded runtime, FFmpeg tools, models, configuration, and license files need a
clear inspectable directory layout. Keeping these components separate also
simplifies diagnostics and future updates.

## Installer

The initial installer target is Inno Setup. Build the installer with:

```powershell
.\scripts\package-inno.ps1
```

Before release, verify that the published app works on a clean Windows x64
machine without global .NET, Python, FFmpeg, models, or development tools.
