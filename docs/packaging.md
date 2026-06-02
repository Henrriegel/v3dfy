# Packaging

## Publish target

v3dfy targets a self-contained Windows x64 publish:

```powershell
.\scripts\publish-win-x64.ps1
```

The script publishes `src/V3dfy.App/V3dfy.App.csproj` to
`artifacts/publish/v3dfy-win-x64` and copies `tools`, `engine`, and `licenses`
next to the app when those directories exist.

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
