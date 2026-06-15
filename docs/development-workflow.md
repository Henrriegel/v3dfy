# Development Workflow

## Repository scope

Work only inside this repository checkout. In the current development
environment that checkout is `C:\dev\v3dfy`, but that path is a development
example only and must not appear in production runtime logic. Keep
implementation tasks scoped to the current request and do not resume or modify
unrelated previous work unless explicitly requested.

## GitHub Desktop workflow

1. Review the current changes in GitHub Desktop before starting an implementation
   task.
2. Create a clear checkpoint commit manually when the worktree is ready.
3. Implement one scoped change.
4. Review the resulting diff in GitHub Desktop.
5. Run build and tests before committing:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
```

6. Commit manually in GitHub Desktop after the validation result is understood.

Do not create branches, commit, or push from automation or scripts unless
explicitly requested.

## Manual app validation workflow

Purpose: validate the real local/offline app layout that users receive after
publish. The app discovers FFmpeg, FFprobe, embedded Python, iw3, models, and
runtime dependencies from the published runtime layout, not from the Debug/bin
developer output.

Wrong flows to avoid:

- Do not use `dotnet run` for visual validation with the engine/models.
- Do not open `src\V3dfy.App\bin\Debug\net10.0-windows\V3dfy.App.exe` for
  engine/model validation.
- Do not manually copy `engine\iw3` into Debug/bin output.

Those flows can produce false System status errors such as Python Missing, iw3
engine Missing, 3D models Missing, or iw3 runtime dependency Missing.

Use this workflow instead.

1. Publish the local Windows x64 runtime layout with the real iw3 bundle:

```powershell
cd C:\dev\v3dfy
pwd

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1 `
  -Iw3BundleRoot C:\v3dfy-iw3-intake\candidate-iw3 `
  -StrictModels `
  -RequireCapabilities
```

2. Validate the published bundle:

```powershell
pwd

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\validate-iw3-bundle.ps1 `
  -BundleRoot C:\dev\v3dfy\artifacts\publish\v3dfy-win-x64\engine\iw3 `
  -StrictModels `
  -RequireCapabilities
```

3. Open the published app for manual validation:

```powershell
pwd

.\artifacts\publish\v3dfy-win-x64\V3dfy.App.exe
```

Expected System status:

- FFmpeg found
- FFprobe found
- Python found
- iw3 engine found
- 3D models found
- iw3 runtime dependency found

Troubleshooting: if System status shows Python, iw3, models, or iw3 runtime
dependency missing, diagnose the publish and validation results first. Do not
fix the symptom by copying files into Debug/bin; that validates the wrong
layout and can hide packaging regressions.
