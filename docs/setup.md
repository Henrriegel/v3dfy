# Developer Setup

## Requirements

- Windows x64 development machine.
- Visual Studio with the **.NET desktop development** workload.
- .NET 10 SDK.
- Git and GitHub Desktop.
- Codex CLI for repository-scoped implementation work.

Run the local environment check from the repository root:

```powershell
.\scripts\check-dev-env.ps1
```

## Offline application rule

Development tools may be installed on the developer machine, but the final app
must not depend on globally installed Python or FFmpeg. The installer must ship
the required runtime, FFmpeg, FFprobe, local AI engine, models, configuration,
and licenses inside the v3dfy installation.
